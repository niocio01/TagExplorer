using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using MaterialDesignThemes.Wpf;
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

public partial class ExtentionButton_VM : Filter
{
    public event EventHandler FileTypeSelected;

    [ObservableProperty] private FileType _fileType;

    public PackIconKind IconKind =>
    Enum.TryParse<PackIconKind>(FileType.IconString, true, out var kind)
        ? kind
        : PackIconKind.FileOutline;


    public ExtentionButton_VM() { }

    public ExtentionButton_VM(FileType fileType)
    {
        FileType = fileType;
    }

}