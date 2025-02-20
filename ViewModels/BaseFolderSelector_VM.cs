using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using TagExplorer.Models;
using Microsoft.WindowsAPICodePack.Dialogs;
using TagExplorer.Data;
using Path = System.IO.Path;

namespace TagExplorer.ViewModels;

public partial class BaseFolderSelector_VM : ObservableObject
{
    [ObservableProperty] private ObservableCollection<BaseFolder> _baseFolders;
    private AppDbContext _db;

    public BaseFolderSelector_VM()
    {
        _db = App.AppHost.Services.GetService<AppDbContext>();
        BaseFolders = new ObservableCollection<BaseFolder>(_db.BaseFolders);
    }

    [RelayCommand]
    public void AddBaseFolder()
    {
        CommonOpenFileDialog dialog = new CommonOpenFileDialog();
        dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        dialog.IsFolderPicker = true;
        if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
        {
            string path = dialog.FileName;
            string FolderName = Path.GetFileName(path);

            if (!_db.BaseFolders.Any(bf => bf.Path == path))
            {
                BaseFolders.Add(new BaseFolder { Name = FolderName, Path = path });
                
            }
        }
    }

    [RelayCommand]
    public void SaveSettings()
    {
        List<BaseFolder> deletedFolders = _db.BaseFolders.Where(bf => !BaseFolders.Contains(bf)).ToList();
        if (deletedFolders.Count > 0)
        {
            _db.BaseFolders.RemoveRange(deletedFolders);
        }

        List<BaseFolder> addedFolders = BaseFolders.Where(bf => !_db.BaseFolders.Contains(bf)).ToList();
        if (addedFolders.Count > 0)
        {
            _db.BaseFolders.AddRange(addedFolders);
        }
        _db.SaveChanges();
    }
}