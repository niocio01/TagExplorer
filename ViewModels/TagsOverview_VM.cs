using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TagExplorer.Data;
using TagExplorer.Models;
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

    public TagsOverview_VM()
    {
        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            throw new InvalidOperationException("Use DI to create ViewModel at runtime.");
    }

    public TagsOverview_VM(AppDbContext db)
    {
        _db = db;

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

        ColorButtonVMs.First(vm => vm.Color == SelectedTag.Color).SelectColor();

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
        IconName = DefaultIconString;
        Color = DefaultTagColor;
        ColorButtonVMs.First(vm => vm.Color == DefaultTagColor).SelectColor();
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
            List<string>? aliases = AliasString?.Split("\r\n").ToList();
            TagDTO newDTOTag = new(Name!, Color!, Description, aliases, IconName!, shortCode: ShortCode);
            _db.Tags.Add(newDTOTag);
            _db.SaveChanges();

            var newTagVm = new Tag_VM(newDTOTag);
            TagVMs.Add(newTagVm);
            newTagVm.TagPressed += TagVM_TagPressed;
        }
        // edit existing tag
        else
        {
            SelectedTag!.Name = Name!;
            SelectedTag.Description = Description;
            SelectedTag.Aliases = AliasString?.Split("\r\n").ToList();
            SelectedTag.IconName = IconName;
            SelectedTag.ShortCode = ShortCode;
            SelectedTag.Color = Color;
            _db.SaveChanges();
            TagVMs.First(t => t.Tag == SelectedTag).UpdateProps();
        }

        NewTagEdit = false;
    }

    [RelayCommand]
    public void DeleteTag()
    {
        if (SelectedTag is null)
            return;
        if (SelectedTag.IsSystemTag)
            return;

        TagVMs.Remove(TagVMs.First(vm => vm.Tag == SelectedTag));
        _db.Tags.Remove(SelectedTag);
        _db.SaveChanges();

        SelectedTagVM = null;
        Name = null;
        Description = null;
        AliasString = null;
        ShortCode = null;
        IconName = null;
        Color = null;
    }

    [RelayCommand]
    public void SelectIconPressed()
    {
        IconSelector_V iconSelector = new IconSelector_V();
        iconSelector.Show();


    }
}

