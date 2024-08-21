using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using TagExplorer.Models;

namespace TagExplorer.ViewModels;

public partial class Tag_VM : ObservableObject
{
    [ObservableProperty] private Tag _tag;
    [ObservableProperty] private bool _isSelected;

    public Tag_VM(Tag tag)
    {
        Tag = tag;
        IsSelected = false;
        WeakReferenceMessenger.Default.Register<TagSelected>(this, (r, m) =>
        {
            if (m.Value != Tag)
            {
                IsSelected = false;
            }
        });
    }

    [RelayCommand]
    public void SelectTag()
    {
        IsSelected = true;
        WeakReferenceMessenger.Default.Send(new TagSelected(Tag));
    }
}

public class TagSelected(Tag tag) : ValueChangedMessage<Tag>(tag);
