using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.WindowsAPICodePack.Dialogs;
using System.Collections.ObjectModel;
using System.IO;
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
        PopulateFolderStatsIfMissing();

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

    private void PopulateFolderStatsIfMissing()
    {
        foreach (var baseFolder in BaseFolders)
        {
            if (baseFolder.StatsTimestampUtc is not null)
            {
                continue;
            }

            if (!Directory.Exists(baseFolder.Path))
            {
                continue;
            }

            var (directoryCount, fileCount, maxDepth) = ScanFolderStats(baseFolder.Path);
            baseFolder.DirectoryCount = directoryCount;
            baseFolder.FileCount = fileCount;
            baseFolder.MaxDepth = maxDepth;
            baseFolder.StatsTimestampUtc = DateTime.UtcNow;
        }
    }

    private static (int directoryCount, int fileCount, int maxDepth) ScanFolderStats(string rootPath)
    {
        var directoryCount = 0;
        var fileCount = 0;
        var maxDepth = 0;

        var pending = new Queue<(string Path, int Depth)>();
        pending.Enqueue((rootPath, 0));

        while (pending.Count > 0)
        {
            var (currentPath, depth) = pending.Dequeue();

            try
            {
                foreach (var _ in Directory.EnumerateFiles(currentPath))
                {
                    fileCount++;
                }
            }
            catch
            {
            }

            try
            {
                foreach (var directory in Directory.EnumerateDirectories(currentPath))
                {
                    directoryCount++;

                    var childDepth = depth + 1;
                    if (childDepth > maxDepth)
                    {
                        maxDepth = childDepth;
                    }

                    pending.Enqueue((directory, childDepth));
                }
            }
            catch
            {
            }
        }

        return (directoryCount, fileCount, maxDepth);
    }
}