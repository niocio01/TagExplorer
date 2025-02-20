using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagExplorer.Data;
using TagExplorer.Models;
using Folder = TagExplorer.Models.Folder;

namespace TagExplorer.ViewModels;

public partial class Explorer_VM : ObservableObject
{
    [ObservableProperty]
    private List<ExplorerItem> _currentFolderItems;

    [ObservableProperty]
    private ExplorerItem _selectedItem;

    private AppDbContext _db;

    public Explorer_VM()
    {
        _db = App.AppHost.Services.GetService<AppDbContext>();

        CurrentFolderItems = new List<ExplorerItem>();
        foreach (FolderBase folderBase in _db.Folders)
        {
            CurrentFolderItems.Add( new Folder(folderBase.Name));
        }
    }

    partial void OnSelectedItemChanged(ExplorerItem value)
    {
        if (value is Folder folder)
        {
            // Do something with the selected folder
        }
    }
}