using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using TagExplorer.Data;
using TagExplorer.Models;
using TagExplorer.Services;
using Folder = TagExplorer.Models.Folder;

namespace TagExplorer.ViewModels;

public partial class Explorer_VM : ObservableObject
{
    public const int HistoryLength = 20;

    [ObservableProperty]
    private FileList_VM _fileList;

    [ObservableProperty]
    private ItemDetails_VM _itemDetails;

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
    public bool IsCacheBuilding => FileList.IsCacheBuilding;

    [ObservableProperty]
    private bool _doRecursiveSearch = true;

    [ObservableProperty]
    private bool _showFolders = true;

    [ObservableProperty]
    private bool _showHiddenFiles = true;

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

    private AppDbContext? _db;

    public Explorer_VM()
    {
        _db = App.AppHost?.Services.GetService<AppDbContext>();
        var tagAssignmentService = App.AppHost?.Services.GetService<TagAssignmentService>();
        var itemSearchService = App.AppHost?.Services.GetService<ItemSearchService>();
        var dataCachingService = App.AppHost?.Services.GetService<DataCachingService>();
        var baseFolders = _db?.BaseFolders.AsNoTracking().ToList() ?? [];

        FileList = new FileList_VM(tagAssignmentService, itemSearchService, dataCachingService);
        FileList.FolderDoubleClicked += FileListFolderDoubleClicked;
        FileList.SelectedItemChanged += FileListSelectedItemChanged;
        FileList.PropertyChanged += FileListPropertyChanged;
        ItemDetails = new ItemDetails_VM(tagAssignmentService);
        _breadcrumbsHistory = new ObservableCollection<List<Folder>>();

        AllFilterTags = new ObservableCollection<FilterTag>();
        List<TagDTO> dtoTags = _db?.Tags
            .Include(tag => tag.Color)
            .ToList() ?? [];
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

        SetCurrentPathToHome(baseFolders);
        AddToHistory(Breadcrumbs.ToList());
    }

    private void FileListSelectedItemChanged(object? sender, ExplorerItem? selectedItem)
    {
        ItemDetails.SelectedItem = selectedItem;
    }

    private void FileListPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileList_VM.IsCacheBuilding))
        {
            OnPropertyChanged(nameof(IsCacheBuilding));
        }
    }

    private void TagFilterTypeChanged(object? sender, FilterTypes e)
    {
        RequiredTagFilterCount = _requiredTagFilters.Count;
        DisallowedTagFilterCount = _disallowedTagFilters.Count;

        ApplyFileListFilters(reloadCurrentFolder: false);
    }

    private void ExtentionFilterTypeChanged(object? sender, FilterTypes e)
    {
        RequiredExtentionFilterCount = _requiredExtentionFilters.Count;
        DisallowedExtentionFilterCount = _disallowedExtentionFilters.Count;

        ApplyFileListFilters(reloadCurrentFolder: false);
    }

    partial void OnDoRecursiveSearchChanged(bool value)
    {
        ApplyFileListFilters(reloadCurrentFolder: true);
    }

    partial void OnShowFoldersChanged(bool value)
    {
        ApplyFileListFilters(reloadCurrentFolder: false);
    }

    partial void OnShowHiddenFilesChanged(bool value)
    {
        ApplyFileListFilters(reloadCurrentFolder: true);
    }


    public void OnItemDoubleClicked()
    {
        FileList.HandleItemDoubleClick();
    }

    private void FileListFolderDoubleClicked(object? sender, Folder folder)
    {
        if (Breadcrumbs.Count == 0)
        {
            return;
        }

        var currentBreadcrumb = Breadcrumbs[ Breadcrumbs.Count - 1 ];

        if (currentBreadcrumb.Name == "BaseFolders")
        {
            Breadcrumbs.Clear();
            Breadcrumbs.Add(folder);
        }
        else
        {
            Breadcrumbs.Add(new Folder(folder.Name, currentBreadcrumb, false));
        }

        Folder newCurrentFolder = Breadcrumbs[ Breadcrumbs.Count - 1 ];

        SetCurrentFolderItems(newCurrentFolder);

        AddToHistory(Breadcrumbs.ToList());
    }

    private void SetCurrentPathToHome(IReadOnlyList<BaseFolder>? baseFolders = null)
    {
        Breadcrumbs =
        [
            new Folder("BaseFolders", "BaseFolders", false)
        ];

        var folders = baseFolders ?? _db?.BaseFolders.AsNoTracking().ToList() ?? [];

        if (folders.Count == 0)
        {
            FileList.SetHomeItems([]);
            return;
        }

        FileList.SetHomeItems(folders);
    }

    private void SetCurrentFolderItems(Folder newCurrentFolder)
    {
        FileList.SetCurrentFolderItems(newCurrentFolder);
    }

    // add the provided breadcrumbs to the history
    // call after changing the breadcrumbs
    private void AddToHistory(List<Folder> breadcrumbs)
    {
        if (breadcrumbs.Count == 0)
        {
            return;
        }

        // delete all entries forward of current
        while (BreadcrumbsHistory.Count - 1 > CurrentHistoryPosition)
        {
            BreadcrumbsHistory.RemoveAt(BreadcrumbsHistory.Count - 1);
        }

        // add the new entry
        BreadcrumbsHistory.Add([.. breadcrumbs]);
        CurrentHistoryPosition = BreadcrumbsHistory.Count - 1;

        // remove the oldest, if necessary
        if (BreadcrumbsHistory.Count > HistoryLength)
        {
            BreadcrumbsHistory.RemoveAt(0);
            CurrentHistoryPosition = Math.Max(0, CurrentHistoryPosition - 1);
        }
    }


    [RelayCommand]
    public void BreadcrumbClick(Folder folder)
    {
        if (Breadcrumbs.Count == 0)
        {
            return;
        }

        if (folder == Breadcrumbs[Breadcrumbs.Count - 1])
        {
            return;
        }

        // remove all breadcrumbs after the clicked one
        while (Breadcrumbs.Count > 0)
        {
            if (Breadcrumbs[Breadcrumbs.Count - 1] == folder)
            {
                break;
            }

            Breadcrumbs.RemoveAt(Breadcrumbs.Count - 1);
        }

        if (Breadcrumbs.Count == 0)
        {
            return;
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

        if (Breadcrumbs.Count > 0)
        {
            SetCurrentFolderItems(Breadcrumbs[Breadcrumbs.Count - 1]);
        }
    }

    [RelayCommand]
    public void GoForward()
    {
        if (!GoForwardAvailable)
            return;

        CurrentHistoryPosition++;
        Breadcrumbs = new ObservableCollection<Folder>(BreadcrumbsHistory[CurrentHistoryPosition]);

        if (Breadcrumbs.Count > 0)
        {
            SetCurrentFolderItems(Breadcrumbs[Breadcrumbs.Count - 1]);
        }
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

    private void ApplyFileListFilters(bool reloadCurrentFolder)
    {
        FileList.UpdateFilters(
            DoRecursiveSearch,
            ShowFolders,
            ShowHiddenFiles,
            _requiredExtentionFilters.Select(filter => filter.FileType.Extension),
            _requiredTagFilters.Where(tag => tag.Id.HasValue).Select(tag => tag.Id!.Value),
            _disallowedTagFilters.Where(tag => tag.Id.HasValue).Select(tag => tag.Id!.Value),
            reloadCurrentFolder);
    }
}