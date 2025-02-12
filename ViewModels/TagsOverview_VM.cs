using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TagExplorer.Data;
using TagExplorer.Models;

namespace TagExplorer.ViewModels;

public partial class TagsOverview_VM : ObservableObject
{
    [ObservableProperty] private ObservableCollection<Tag_VM> _tagsVMs;
    [ObservableProperty] private Tag _selectedTag;

    private AppDbContext _db;

    public TagsOverview_VM()
    {
        if (!DesignerProperties.GetIsInDesignMode(new DependencyObject()))
            throw new InvalidOperationException("Use DI to create ViewModel at runtime.");
    }

    public TagsOverview_VM(AppDbContext db)
    {
        _db = db;
        SelectedTag = null;

        TagsVMs = new ObservableCollection<Tag_VM>();

        UpdateTagsVMs();

        // WeakReferenceMessenger.Default.Register<ListLoadingStatusChanged>(this, (r, m) =>
        // {
        //     if (m.Value == true)
        //     {
        //         UpdateTagsVMs();
        //     }
        // });

        WeakReferenceMessenger.Default.Register<TagSelected>(this, (r, m) =>
        {
            SelectedTag = m.Value;
        });
    }

    private void UpdateTagsVMs()
    {
        TagsVMs.Clear();
        var tags = _db.Tags.Include(tag => tag.Color).ToList();
        foreach (Tag tag in tags)
        {
            TagsVMs.Add(new Tag_VM(tag));
        }
    }
}

