using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using TagExplorer.Data;
using TagExplorer.Models.Messages;
using TagExplorer.Services;
using TagExplorer.Views;
using Color = TagExplorer.Data.Color;

namespace TagExplorer.ViewModels;

public partial class TagsOverview_VM : ObservableValidator
{
    [ObservableProperty]
    private ObservableCollection<Tag_VM> _tagVMs;

    [ObservableProperty]
    private ObservableCollection<ColorButton_VM> _colorButtonVMs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(SelectedTag),
   [
        nameof(EditEnabled),
        nameof(SomeTagIsSelected),
        nameof(CanResetToGenerated),
        nameof(DeleteButtonIcon),
        nameof(DeleteButtonToolTip),
    ])]

    private Tag_VM? _selectedTagVM;

    #region SslectedTagProperties

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required]
    [StringLength(50)]
    private string? _name;

    [ObservableProperty]
    [StringLength(200)]
    [NotifyDataErrorInfo]
    private string? _description;

    [ObservableProperty]
    private string? _aliasString;

    [ObservableProperty]
    private string? _iconName;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [StringLength(8)]
    private string? _shortCode;

    [NotifyDataErrorInfo]
    [ObservableProperty]
    [Required]
    private Color? _color;

    [ObservableProperty]
    private ObservableCollection<Tag_VM> _childrenVMs;

    #endregion
    private Color DefaultTagColor;
    private String DefaultIconString = "Help";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EditEnabled))]
    private bool _newTagEdit;

    /// <summary>
    /// Whether the tag list shows the tags created automatically for subfolders.
    /// </summary>
    [ObservableProperty]
    private bool _showAutoCreatedTags = true;

    /// <summary>True if the selected tag is a stored (customised or hidden) subfolder tag that can be reset.</summary>
    public bool CanResetToGenerated => SelectedTag is { AutoSourcePath: not null, IsGenerated: false };

    /// <summary>Whether the tag list also shows hidden subfolder tags.</summary>
    [ObservableProperty]
    private bool _showHiddenTags;

    private bool SelectedIsHidden => SelectedTag is { AutoSourcePath: not null, IsArchived: true };

    // Subfolder tags are hidden instead of deleted, else their rule would generate them again.
    private bool SelectedIsHideable => SelectedTag is { AutoSourcePath: not null, IsArchived: false } && SelectedTagVM?.IsOrphaned != true;

    public string DeleteButtonIcon => SelectedIsHidden ? "Eye" : SelectedIsHideable ? "EyeOff" : "Delete";

    public string DeleteButtonToolTip => SelectedIsHidden
        ? "Show this subfolder tag again"
        : SelectedIsHideable ? "Hide this subfolder tag" : "Delete tag";

    public TagDTO? SelectedTag => SelectedTagVM?.Tag;
    public bool SomeTagIsSelected => SelectedTag is not null;
    public bool EditEnabled => SomeTagIsSelected || NewTagEdit;

    private AppDbContext _db;
    private readonly DataCachingService? _dataCachingService;

    // The thread owning TagVMs; tag messages can come from cache-build worker threads.
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

    public TagsOverview_VM()
    {
        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            throw new InvalidOperationException("Use DI to create ViewModel at runtime.");
    }

    public TagsOverview_VM(AppDbContext db, DataCachingService dataCachingService)
    {
        _db = db;
        _dataCachingService = dataCachingService;

        DefaultTagColor = _db.Colors.First(c => c.Name == "Blue");

        ColorButtonVMs = new ObservableCollection<ColorButton_VM>();
        foreach (Color color in _db.Colors)
        {
            var colorButtonVM = new ColorButton_VM(color);
            ColorButtonVMs.Add(colorButtonVM);
            colorButtonVM.ColorPressed += ColorButtonVM_ColorPressed;
        }

        UpdateTagsVMs();
        ChildrenVMs = new ObservableCollection<Tag_VM>();

        // Tags also change outside this view (generated child tags after a cache build, tags stored on first use).
        // The message can come from a worker thread.
        WeakReferenceMessenger.Default.Register<TagsChangedMessage>(this, static (recipient, message) =>
        {
            var vm = (TagsOverview_VM)recipient;
            if (!vm._dispatcher.CheckAccess())
            {
                _ = vm._dispatcher.InvokeAsync(vm.SyncTagVMs);
                return;
            }

            vm.SyncTagVMs();
        });
    }

    partial void OnTagVMsChanged(ObservableCollection<Tag_VM> value)
    {
        CollectionViewSource.GetDefaultView(value).Filter = item => item is Tag_VM vm
            && (vm.IsHidden ? ShowHiddenTags : ShowAutoCreatedTags || !vm.IsSubfolderTag);
    }

    partial void OnShowAutoCreatedTagsChanged(bool value)
    {
        CollectionViewSource.GetDefaultView(TagVMs).Refresh();
    }

    partial void OnShowHiddenTagsChanged(bool value)
    {
        CollectionViewSource.GetDefaultView(TagVMs).Refresh();
    }

    /// <summary>
    /// Brings the list in line with the stored tags (DB) and the generated child tags (cache).
    /// </summary>
    private void SyncTagVMs()
    {
        // Rows changed through another DbContext (e.g. a subfolder tag hidden by DataCachingService) are reloaded,
        // and their entries rebuilt; tracked instances would otherwise keep the old values.
        var updatedAtById = _db.Tags.AsNoTracking().Select(t => new { t.Id, t.UpdatedAtUtc }).ToDictionary(t => t.Id, t => t.UpdatedAtUtc);
        var changedEntries = _db.ChangeTracker.Entries<TagDTO>()
            .Where(entry => entry.State == EntityState.Unchanged
                && updatedAtById.TryGetValue(entry.Entity.Id, out var updatedAt)
                && updatedAt != entry.Entity.UpdatedAtUtc)
            .ToList();

        foreach (var entry in changedEntries)
        {
            entry.Reload();
            foreach (var changedVM in TagVMs.Where(vm => vm.Tag.Id == entry.Entity.Id).ToList())
            {
                changedVM.TagPressed -= TagVM_TagPressed;
                TagVMs.Remove(changedVM);
                if (changedVM == SelectedTagVM)
                {
                    ClearEditor();
                }
            }
        }

        var storedTags = _db.Tags.Include(tag => tag.Color).ToList();
        var generatedTags = _dataCachingService?.Tags.Where(tag => tag.IsGenerated).ToList() ?? [];
        var tagIds = storedTags.Select(t => t.Id).Concat(generatedTags.Select(t => t.Id!.Value)).ToHashSet();

        foreach (var removedVM in TagVMs.Where(vm => !tagIds.Contains(vm.Tag.Id)).ToList())
        {
            removedVM.TagPressed -= TagVM_TagPressed;
            TagVMs.Remove(removedVM);
            if (removedVM == SelectedTagVM)
            {
                ClearEditor();
            }
        }

        var shownIds = TagVMs.Select(vm => vm.Tag.Id).ToHashSet();
        var addedVMs = storedTags.Where(tag => !shownIds.Contains(tag.Id)).Select(tag => new Tag_VM(tag))
            .Concat(generatedTags.Where(tag => !shownIds.Contains(tag.Id!.Value)).Select(Tag_VM.ForGenerated));

        foreach (var tagVM in addedVMs)
        {
            tagVM.TagPressed += TagVM_TagPressed;
            TagVMs.Add(tagVM);
        }

        foreach (var tagVM in TagVMs)
        {
            tagVM.IsOrphaned = tagVM.Tag is { AutoSourcePath: not null, IsGenerated: false, IsArchived: false }
                && _dataCachingService?.IsActiveChildTag(tagVM.Tag.Id) == false;
        }
    }

    private void ColorButtonVM_ColorPressed(object? sender, EventArgs e)
    {
        if (sender is not ColorButton_VM colorButtonVM)
            return;

        foreach (ColorButton_VM selectedButton in ColorButtonVMs.Where(c => c.IsSelected))
        {
            selectedButton.IsSelected = false;
        }

        colorButtonVM.IsSelected = true;
        Color = colorButtonVM.Color;
    }

    private void UpdateTagsVMs()
    {
        TagVMs = new ObservableCollection<Tag_VM>();
        SyncTagVMs();
    }

    private void TagVM_TagPressed(object? sender, EventArgs e)
    {
        if (sender is not Tag_VM tagVM)
            return;

        // deselect old tag
        if (SelectedTagVM != null)
        {
            SelectedTagVM.IsSelected = false;
        }

        // set new tag
        SelectedTagVM = TagVMs.First(t => t.Tag == tagVM.Tag);

        // set as selected
        SelectedTagVM.IsSelected = true;

        SetPrivateTagProps();

        ChildrenVMs.Clear();
        if (SelectedTag.Children is null)
        {
            return;
        }

        foreach (TagDTO child in SelectedTag.Children)
        {
            ChildrenVMs.Add(new Tag_VM(child));
        }
    }

    private void SetPrivateTagProps()
    {
        Name = SelectedTag?.Name;
        Description = SelectedTag?.Description;
        AliasString = SelectedTag?.Aliases == null ? "" : string.Join("\r\n", SelectedTag.Aliases);
        ShortCode = SelectedTag?.ShortCode;
        // Keep the selected tag's own icon and colour, so saving other changes doesn't reset them.
        IconName = SelectedTag?.IconName ?? DefaultIconString;
        // Match by id: generated tags carry a Color instance from another DbContext. Use this context's instance,
        // so a later save references a tracked Color.
        var colorButtonVM = ColorButtonVMs.First(vm => vm.Color.Id == (SelectedTag?.Color ?? DefaultTagColor).Id);
        Color = colorButtonVM.Color;
        colorButtonVM.SelectColor();
    }

    private void ClearEditor()
    {
        SelectedTagVM = null;
        NewTagEdit = false;
        Name = null;
        Description = null;
        AliasString = null;
        ShortCode = null;
        IconName = null;
        Color = null;
        ChildrenVMs.Clear();

        foreach (ColorButton_VM colorButtonVM in ColorButtonVMs)
        {
            colorButtonVM.IsSelected = false;
        }

        // Setting the required fields to null must not show validation errors on the empty editor.
        ClearErrors();
    }

    [RelayCommand]
    public void AddTag()
    {
        if (SelectedTag is not null)
        {
            TagVMs.First(vm => vm.Tag == SelectedTag).IsSelected = false;
        }

        SelectedTagVM = null;
        NewTagEdit = true;
        SetPrivateTagProps();
    }

    [RelayCommand]
    public void SaveTagChanges()
    {
        ValidateAllProperties();
        if (HasErrors)
            return;

        if (NewTagEdit)
        {
            TagDTO newDTOTag = new(Name!, Color!, Description, ParseAliases(), IconName!, shortCode: ShortCode);
            _db.Tags.Add(newDTOTag);
            _db.SaveChanges();
            // UpsertTag sends TagsChangedMessage; SyncTagVMs adds the new tag to TagVMs.
            _dataCachingService?.UpsertTag(newDTOTag);
        }
        // edit existing tag
        else
        {
            if (SelectedTag!.IsGenerated && !StoreSelectedGeneratedTag())
            {
                return;
            }

            SelectedTag!.Name = Name!;
            SelectedTag.Description = Description;
            SelectedTag.Aliases = ParseAliases();
            SelectedTag.IconName = IconName;
            SelectedTag.ShortCode = ShortCode;
            SelectedTag.Color = Color;
            _db.SaveChanges();
            _dataCachingService?.UpsertTag(SelectedTag);
            TagVMs.First(t => t.Tag == SelectedTag).UpdateProps();
        }

        NewTagEdit = false;
    }

    /// <summary>
    /// Stores the selected generated child tag before it is edited, then selects its stored version. The editor
    /// values are kept, since the list sync replaces the selected entry.
    /// </summary>
    private bool StoreSelectedGeneratedTag()
    {
        if (_dataCachingService?.GetTag(SelectedTag!.Id) is not Models.Tag generatedTag)
        {
            return false;
        }

        var (name, description, aliasString, iconName, shortCode, color) = (Name, Description, AliasString, IconName, ShortCode, Color);

        var storedTag = _dataCachingService.StoreGeneratedTag(generatedTag);
        var storedVM = TagVMs.FirstOrDefault(vm => vm.Tag.Id == storedTag.Id);
        if (storedVM is null)
        {
            return false;
        }

        storedVM.PressTag();
        (Name, Description, AliasString, IconName, ShortCode, Color) = (name, description, aliasString, iconName, shortCode, color);
        return true;
    }

    [RelayCommand]
    public void ResetToGenerated()
    {
        if (!CanResetToGenerated || _dataCachingService is null)
            return;

        TagDTO tag = SelectedTag!;
        var result = MessageBox.Show(
            $"Reset the tag of \"{tag.AutoSourcePath}\" to the generated one? Its name, aliases and other changes are lost, and it is removed from the items it was assigned to.",
            "Reset tag", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes)
            return;

        _dataCachingService.DeleteTagWithChildren(tag.Id);
        ClearEditor();
    }

    // One alias per line; blank lines are ignored, and no aliases are stored as null instead of [""].
    private List<string>? ParseAliases()
    {
        List<string>? aliases = AliasString?
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();

        return aliases is { Count: > 0 } ? aliases : null;
    }

    [RelayCommand]
    public void DeleteTag()
    {
        if (SelectedTag is null)
            return;
        if (SelectedTag.IsSystemTag)
            return;

        if (_dataCachingService is null)
            return;

        TagDTO tag = SelectedTag;

        if (SelectedIsHidden)
        {
            _dataCachingService.UnhideChildTag(tag.Id);
            ClearEditor();
            return;
        }

        if (SelectedIsHideable)
        {
            HideSelectedSubfolderTag(tag);
            return;
        }

        var tagIds = _dataCachingService.GetTagAndDescendantIds(tag.Id).ToList();
        int assignmentCount = _db.TagAssignments.Count(a =>
            ((a.TagId != null && tagIds.Contains(a.TagId.Value))
                || (a.AutoRuleParentTagId != null && tagIds.Contains(a.AutoRuleParentTagId.Value)))
            && !a.IsArchived && a.Enabled);

        string message = $"Delete the tag \"{tag.Name}\"?";
        if (tagIds.Count > 1)
            message += $" Its {tagIds.Count - 1} child tag(s) are deleted too.";
        if (assignmentCount > 0)
            message += $" {assignmentCount} assignment(s) on files or folders are removed.";

        MessageBoxResult result = MessageBox.Show(message, "Delete tag", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes)
            return;

        // Also removes the deleted tags from TagVMs (SyncTagVMs) and clears the editor.
        _dataCachingService.DeleteTagWithChildren(tag.Id);
        ClearEditor();
    }

    private void HideSelectedSubfolderTag(TagDTO tag)
    {
        if (_dataCachingService?.GetTag(tag.Id) is not Models.Tag cachedTag)
            return;

        string message = $"Hide the tag \"{tag.Name}\"? The folder \"{tag.AutoSourcePath}\" gets no tag from its rule any more.";
        if (!tag.IsGenerated)
            message += " The tag is also removed from the items it was assigned to.";
        message += " Tick \"Show hidden tags\" to find it and show it again.";

        MessageBoxResult result = MessageBox.Show(message, "Hide tag", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes)
            return;

        _dataCachingService.HideChildTag(cachedTag);
        ClearEditor();
    }

    [RelayCommand]
    public void SelectIconPressed()
    {
        IconSelector_V iconSelector = new IconSelector_V();
        iconSelector.Show();


    }
}

