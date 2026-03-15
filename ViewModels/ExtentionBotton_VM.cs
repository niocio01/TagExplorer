using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TagExplorer.Data;
using TagExplorer.Models;

namespace TagExplorer.ViewModels;

public partial class ExtentionButton_VM : ObservableObject
{
    public event EventHandler FileTypeSelected;

    [ObservableProperty] private FileType _fileType;
    [ObservableProperty] private bool _isSelected;

    public ExtentionButton_VM() { }

    public ExtentionButton_VM(FileType fileType)
    {
        FileType = fileType;
    }

    [RelayCommand]
    public void SelectFileType()
    {
        IsSelected = !IsSelected;
        FileTypeSelected?.Invoke(this, EventArgs.Empty);
    }
}