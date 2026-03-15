using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using TagExplorer.Data;
using TagExplorer.Models;
using File = TagExplorer.Models.File;
using Folder = TagExplorer.Models.Folder;

namespace TagExplorer.ViewModels;

public partial class Explorer_VM : ObservableObject
{
    public const int HistoryLength = 20;

    [ObservableProperty]
    private ObservableCollection<ExplorerItem> _currentFolderItems;

    [ObservableProperty]
    private ObservableCollection<Folder> _breadcrumbs;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GoBackAvailable))]
    private ObservableCollection<List<Folder>> _breadcrumbsHistory;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GoForwardAvailable), nameof(GoBackAvailable))]
    private int _currentHistoryPosition;
    
    public bool GoBackAvailable => CurrentHistoryPosition >= 1;
    public bool GoForwardAvailable => CurrentHistoryPosition < BreadcrumbsHistory.Count-1;

    [ObservableProperty]
    private ExplorerItem? _selectedItem;

    [ObservableProperty]
    private ObservableCollection<FilterTag> _allFilterTags;
    private List<FilterTag> _requiredTagFilters => AllFilterTags.Where(t => t.FilterType == FilterTypes.Required).ToList();
    private List<FilterTag> _disallowedTagFilters => AllFilterTags.Where(t => t.FilterType == FilterTypes.Disallowed).ToList();

    [ObservableProperty]
    private int _requiredTagFilterCount;

    [ObservableProperty]
    private int _disallowedTagFilterCount;

    [ObservableProperty]
    private ObservableCollection<ExtentionButton_VM> _AllFilterExtention_VMs;

    private List<ExtentionButton_VM> _requiredExtentionFilters => AllFilterExtention_VMs.Where(e => e.FilterType == FilterTypes.Required).ToList();
    private List<ExtentionButton_VM> _disallowedExtentionFilters => AllFilterExtention_VMs.Where(t => t.FilterType == FilterTypes.Disallowed).ToList();

    [ObservableProperty]
    private int _requiredExtentionFilterCount;

    [ObservableProperty]
    private int _disallowedExtentionFilterCount;



    private AppDbContext _db;

    public Explorer_VM()
    {
        _db = App.AppHost.Services.GetService<AppDbContext>();

        CurrentFolderItems = new ObservableCollection<ExplorerItem>();

        _breadcrumbsHistory = new ObservableCollection<List<Folder>>();

        AllFilterTags = new ObservableCollection<FilterTag>();
        List<TagDTO> dtoTags = _db.Tags
            .Include(tag => tag.Color)
            .ToList();
        foreach (TagDTO tagDTO in dtoTags)
        {
            var ft = new FilterTag(tagDTO);
            AllFilterTags.Add(ft);
            ft.FilterTypeChanged += TagFilterTypeChanged;
        }

        AllFilterExtention_VMs = new ObservableCollection<ExtentionButton_VM>();
        foreach (FileType fileType in FileTypes.Types)
        {
            var extentionButton_VM = new ExtentionButton_VM(fileType);
            AllFilterExtention_VMs.Add(extentionButton_VM);
            extentionButton_VM.FilterTypeChanged += ExtentionFilterTypeChanged;
        }

        SetCurrentPathToHome();
        AddToHistory(Breadcrumbs.ToList());
    }

    private void TagFilterTypeChanged(object? sender, FilterTypes e)
    {
        RequiredTagFilterCount = _requiredTagFilters.Count;
        DisallowedTagFilterCount = _disallowedTagFilters.Count;
    }

    private void ExtentionFilterTypeChanged(object? sender, FilterTypes e)
    {
        RequiredExtentionFilterCount = _requiredExtentionFilters.Count;
        DisallowedExtentionFilterCount = _disallowedExtentionFilters.Count;
    }

    partial void OnSelectedItemChanged(ExplorerItem? value)
    {
        
    }


    public void OnItemDoubleClicked()
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

        AddToHistory(Breadcrumbs.ToList());
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
            CurrentFolderItems.Add(new File(Path.GetFileName(file), Path.GetExtension(file)));
        }
    }

    // add the provided breadcrumbs to the history
    // call after changing the breadcrumbs
    private void AddToHistory(List<Folder> breadcrumbs)
    {
        // delete all entries forward of current
        if (GoForwardAvailable)
        {
            for (int i = 0; i < BreadcrumbsHistory.Count; i++)
            {
                if (GoForwardAvailable)
                {
                    BreadcrumbsHistory.Remove(BreadcrumbsHistory.Last());
                }
            }
        }

        // add the new entry
        BreadcrumbsHistory.Add(Breadcrumbs.ToList());
        CurrentHistoryPosition = BreadcrumbsHistory.Count-1;

        // remove the oldest, if necessary
        if (BreadcrumbsHistory.Count > HistoryLength)
        {
            BreadcrumbsHistory.RemoveAt(0);
        }
    }


    [RelayCommand]
    public void BreadcrumbClick(Folder folder)
    {
        if (folder == Breadcrumbs.Last())
        {
            return;
        }

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

        AddToHistory(Breadcrumbs.ToList());
    }

    [RelayCommand]
    public void BreadcrumbHomeClicked()
    {
        SetCurrentPathToHome();
    }

    [RelayCommand]
    public void GoBack()
    {
        if (!GoBackAvailable)
            return;

        CurrentHistoryPosition --;
        Breadcrumbs = new ObservableCollection<Folder>(BreadcrumbsHistory[CurrentHistoryPosition]);

        SetCurrentFolderItems(Breadcrumbs.Last());
    }

    [RelayCommand]
    public void GoForward()
    {
        if (!GoForwardAvailable)
            return;

        CurrentHistoryPosition++;
        Breadcrumbs = new ObservableCollection<Folder>(BreadcrumbsHistory[CurrentHistoryPosition]);

        SetCurrentFolderItems(Breadcrumbs.Last());
    }

    [RelayCommand]
    public void ClearExtentionFilters()
    {
        foreach (var extentionButton_VM in AllFilterExtention_VMs)
        {
            extentionButton_VM.FilterType = FilterTypes.None;
        }
    }

    [RelayCommand]
    public void ClearTagsFilters()
    {
        foreach (var filterTag in AllFilterTags)
        {
            filterTag.FilterType = FilterTypes.None;
        }
    }
}