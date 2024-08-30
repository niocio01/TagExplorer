using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using TagExplorer.Models;

namespace TagExplorer.ViewModels;

public partial class TagsOverview_VM : ObservableObject
{
    [ObservableProperty] private TagTable _tagTable = Main.TagTable;

    [ObservableProperty] private ObservableCollection<Tag_VM> _tagsVMs;
    [ObservableProperty] private Tag _selectedTag;

    public TagsOverview_VM()
    {
        SelectedTag = null;

        TagsVMs = new ObservableCollection<Tag_VM>();

        WeakReferenceMessenger.Default.Register<ListLoadingStatusChanged>(this, (r, m) =>
        {
            if (m.Value == true)
            {
                UpdateTagsVMs();
            }
        });

        WeakReferenceMessenger.Default.Register<TagSelected>(this, (r, m) =>
        {
            SelectedTag = m.Value;
        });
    }

    private void UpdateTagsVMs()
    {
        TagsVMs.Clear();
        foreach (var tag in TagTable.Data)
        {
            TagsVMs.Add(new Tag_VM(tag));
        }
    }
}

