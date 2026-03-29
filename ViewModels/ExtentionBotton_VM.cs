using CommunityToolkit.Mvvm.ComponentModel;
using MaterialDesignThemes.Wpf;
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