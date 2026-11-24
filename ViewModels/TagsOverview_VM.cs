using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows;
using TagExplorer.Data;
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

    public TagDTO? SelectedTag => SelectedTagVM?.Tag;
    public bool SomeTagIsSelected => SelectedTag is not null;
    public bool EditEnabled => SomeTagIsSelected || NewTagEdit;

    private AppDbContext _db;
    private readonly DataCachingService? _dataCachingService;

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
        List<TagDTO> dtoTags = _db.Tags
            .Include(tag => tag.Color)
            .ToList();

        TagVMs = new ObservableCollection<Tag_VM>();
        foreach (TagDTO tag in dtoTags)
        {
            TagVMs.Add(new Tag_VM(tag));
        }

        // add clicked event handler
        foreach (Tag_VM tagVM in TagVMs)
        {
            tagVM.TagPressed += TagVM_TagPressed;
        }
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
        Color = SelectedTag?.Color ?? DefaultTagColor;
        ColorButtonVMs.First(vm => vm.Color == Color).SelectColor();
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
            _dataCachingService?.UpsertTag(newDTOTag);

            var newTagVm = new Tag_VM(newDTOTag);
            TagVMs.Add(newTagVm);
            newTagVm.TagPressed += TagVM_TagPressed;
        }
        // edit existing tag
        else
        {
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

        TagDTO tag = SelectedTag;
        int assignedItemCount = _db.TagAssignments.Count(a => a.TagId == tag.Id && !a.IsArchived && a.Enabled);

        string message = assignedItemCount == 0
            ? $"Delete the tag \"{tag.Name}\"?"
            : $"Delete the tag \"{tag.Name}\"? It is assigned to {assignedItemCount} item(s); it will be removed from them.";

        MessageBoxResult result = MessageBox.Show(message, "Delete tag", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes)
            return;

        RemoveAssignmentsOfTag(tag.Id);

        TagVMs.Remove(TagVMs.First(vm => vm.Tag == tag));
        _db.Tags.Remove(tag);
        _db.SaveChanges();
        _dataCachingService?.RemoveTag(tag.Id);

        ClearEditor();
    }

    // There is no foreign key from TagAssignments to Tags, so assignments of a deleted tag must be removed here.
    private void RemoveAssignmentsOfTag(int tagId)
    {
        // Goes through the cache so badges and filters update; this archives the assignments.
        if (_dataCachingService is not null)
        {
            foreach (var application in _dataCachingService.GetTagApplicationsForTag(tagId))
            {
                _dataCachingService.RemoveTagApplication(application);
            }
        }

        // Assignments whose item isn't in the cache (e.g. a missing file) are archived directly.
        _db.TagAssignments
            .Where(a => a.TagId == tagId && !a.IsArchived)
            .ExecuteUpdate(setters => setters
                .SetProperty(a => a.IsArchived, true)
                .SetProperty(a => a.Enabled, false)
                .SetProperty(a => a.UpdatedAtUtc, DateTime.UtcNow));
    }

    [RelayCommand]
    public void SelectIconPressed()
    {
        IconSelector_V iconSelector = new IconSelector_V();
        iconSelector.Show();


    }
}

