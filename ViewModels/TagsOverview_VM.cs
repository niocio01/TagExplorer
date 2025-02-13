using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using TagExplorer.Data;
using TagExplorer.Models;
using TagExplorer.Views;
using Color = TagExplorer.Data.Color;

namespace TagExplorer.ViewModels;

public partial class TagsOverview_VM : ObservableObject
{

    private List<Tag> Tags;
    [ObservableProperty] private ObservableCollection<Tag_VM> _tagViewsModels;
    [ObservableProperty] private ObservableCollection<ColorButton_VM> _colorButtonVMs;
    [ObservableProperty] private Tag? _selectedTag;
    [ObservableProperty] private ObservableCollection<Tag_VM> _selectedTagChildrenVMs;
    [ObservableProperty] private string _selectedTagAliasString; // managed manually

    private AppDbContext _db;

    public TagsOverview_VM()
    {
        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            throw new InvalidOperationException("Use DI to create ViewModel at runtime.");
    }

    public TagsOverview_VM(AppDbContext db)
    {
        _db = db;

        ColorButtonVMs = new ObservableCollection<ColorButton_VM>();
        foreach (Color color in _db.Colors)
        {
            ColorButtonVMs.Add(new ColorButton_VM(color));
        }

        SelectedTag = null;
        SelectedTagChildrenVMs = new ObservableCollection<Tag_VM>();
        SelectedTagAliasString = "";

        UpdateTagsVMs();

        WeakReferenceMessenger.Default.Register<TagSelected>(this, SelectNewTag);
    }

    private void SelectNewTag(object recipient, TagSelected message)
    {
        SelectedTag = message.Value;
        ColorButtonVMs.First(vm => vm.Color == SelectedTag.Color).SelectColor();
        SelectedTagAliasString = SelectedTag.Aliases == null ? "" : string.Join("/n", SelectedTag.Aliases);

        SelectedTagChildrenVMs.Clear();
        if (SelectedTag.Children is null)
        {
            return;
        }

        foreach (Tag child in SelectedTag.Children)
        {
            SelectedTagChildrenVMs.Add(new Tag_VM(child));
        }
    }

    private void UpdateTagsVMs()
    {
        List<TagDTO> tags = _db.Tags
            .Include(tag => tag.Color)
            .ToList();

        Tags = new List<Tag>();

        // create Tags from DTOs
        foreach (TagDTO tag in tags)
        {
            Tags.Add(new Tag(tag));
        }

        // add children and parents to Tags
        foreach (TagDTO tagDTO in tags)
        {
            if (tagDTO.Children is not null && tagDTO.Children.Count > 0)
            {
                foreach (TagDTO childDTO in tagDTO.Children!)
                {
                    Tags.First(t => t.DTO == tagDTO).Children.Add(Tags.First(t => t.DTO == childDTO));
                }
            }

            if (tagDTO.Parent is not null)
            {
                Tags.First(t => t.DTO == tagDTO).Parent = Tags.First(t => t.DTO == tagDTO.Parent);
            }
        }

        TagViewsModels = new ObservableCollection<Tag_VM>();
        foreach (Tag tag in Tags)
        {
            TagViewsModels.Add(new Tag_VM(tag));
        }
    }

    [RelayCommand]
    public void SaveTagChanges()
    {
        if (SelectedTag is null) 
            return;
        
        SelectedTag.Aliases = string.IsNullOrEmpty(SelectedTagAliasString) ? null : SelectedTagAliasString.Split("/n").ToList();

        SelectedTag.Color = ColorButtonVMs.First(vm => vm.IsSelected).Color;

        _db.SaveChanges();
    }
}

