using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagExplorer.Data;
using TagExplorer.Models;
using File = TagExplorer.Models.File;
using Folder = TagExplorer.Models.Folder;

namespace TagExplorer.ViewModels;

public partial class Explorer_VM : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ExplorerItem> _currentFolderItems;

    [ObservableProperty]
    private ObservableCollection<Folder> _breadcrumbs;

    [ObservableProperty]
    private ExplorerItem? _selectedItem;

    private AppDbContext _db;

    public Explorer_VM()
    {
        _db = App.AppHost.Services.GetService<AppDbContext>();

        CurrentFolderItems = new ObservableCollection<ExplorerItem>();

        SetCurrentPathToHome();
    }
    

    partial void OnSelectedItemChanged(ExplorerItem? value)
    {
        if (SelectedItem == null)
        {
            return;
        }

        var selectedItem = SelectedItem;
        SelectedItem = null;

        if (selectedItem is Folder folder)
        {
            if (Breadcrumbs.Last().Name == "BaseFolders")
            {
                Breadcrumbs.Clear();
                Breadcrumbs.Add(folder);
            }
            else
            {
                Breadcrumbs.Add(new Folder(folder.Name, Breadcrumbs.Last()));
            }

            Folder newCurrentFolder = Breadcrumbs.Last();

            SetCurrentFolderItems(newCurrentFolder);
        }
    }

    private void SetCurrentFolderItems(Folder newCurrentFolder)
    {
        if (newCurrentFolder.Name == "BaseFolders")
        {
            SetCurrentPathToHome();
            return;
        }

        CurrentFolderItems.Clear();
        var directories = Directory.GetDirectories(newCurrentFolder.Path);
        foreach (var directory in directories) {
            CurrentFolderItems.Add(new Folder(Path.GetFileName(directory), newCurrentFolder));
        }

        var files = Directory.GetFiles(newCurrentFolder.Path);
        foreach (var file in files) {
            CurrentFolderItems.Add(new File(Path.GetFileNameWithoutExtension(file), Path.GetExtension(file)));
        }
    }

    [RelayCommand]
    public void BreadcrumbClick(Folder folder)
    {
        // remove all breadcrumbs after the clicked one
        for (int i = 0; i < Breadcrumbs.Count; i++)
        {
            if (Breadcrumbs.Last() == folder)
            {
                break;
            }
            Breadcrumbs.Remove(Breadcrumbs.Last());
        }

        if (folder is Folder)
        {
            SetCurrentFolderItems(folder);
        }
    }

    [RelayCommand]
    public void BreadcrumbHomeClicked()
    {
        SetCurrentPathToHome();
    }

    private void SetCurrentPathToHome()
    {
        Breadcrumbs =
        [
            new Folder("BaseFolders", "BaseFolders")
        ];

        CurrentFolderItems.Clear();
        foreach (FolderBase folderBase in _db.Folders)
        {
            CurrentFolderItems.Add(new Folder(folderBase.Path, folderBase.Name));
        }
    }
}