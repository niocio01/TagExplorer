using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TagExplorer.Models;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Windows.Shapes;
using Path = System.IO.Path;

namespace TagExplorer.ViewModels;

public partial class BaseFolderSelector_VM : ObservableObject
{
    [ObservableProperty] private ObservableCollection<BaseFolder> _baseFolders;

    public BaseFolderSelector_VM()
    {
        BaseFolders = Main.BaseFolderTable.Data;
    }

    [RelayCommand]
    public void AddBaseFolder()
    {
        CommonOpenFileDialog dialog = new CommonOpenFileDialog();
        dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        dialog.IsFolderPicker = true;
        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            BaseFolders.Add(new BaseFolder(dialog.FileName, Path.GetFileName(dialog.FileName)));
            OnPropertyChanged(nameof(BaseFolders));
        }
    }

    [RelayCommand]
    public void SaveSettings()
    {
        DBConnector.SaveBaseFolders();
    }
}