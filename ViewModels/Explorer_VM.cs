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
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using TagExplorer.Data;
using TagExplorer.Models;
using TagExplorer.Services;
using File = TagExplorer.Models.File;
using Folder = TagExplorer.Models.Folder;

namespace TagExplorer.ViewModels;

public partial class Explorer_VM : ObservableObject
{
    public const int HistoryLength = 20;

    [ObservableProperty]
    private ObservableCollection<ExplorerItem> _currentFolderItems;

    [ObservableProperty]
    private ObservableCollection<ExplorerItem> _filteredFolderItems;

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
    private bool _doRecursiveSearch = true;

    [ObservableProperty]
    private bool _showFolders = true;

    [ObservableProperty]
    private bool _showHiddenFiles = false;

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

    [ObservableProperty]
    private bool _isSearching;

    [ObservableProperty]
    private int _scannedFolderCount;

    [ObservableProperty]
    private int _scannedFileCount;

    [ObservableProperty]
    private int _scannedDepth;

    [ObservableProperty]
    private int _filteredFolderCount;

    [ObservableProperty]
    private int _filteredFileCount;

    [ObservableProperty]
    private double _estimatedSearchProgress;

    private AppDbContext? _db;
    private readonly ItemSearchService _itemSearchService = new();
    private CancellationTokenSource? _itemSearchCts;
    private readonly HashSet<string> _requiredExtensionsCache = new(StringComparer.OrdinalIgnoreCase);

    public Explorer_VM()
    {
        _db = App.AppHost?.Services.GetService<AppDbContext>();

        CurrentFolderItems = new ObservableCollection<ExplorerItem>();
        FilteredFolderItems = new ObservableCollection<ExplorerItem>();
        FilteredFolderItems.CollectionChanged += (_, _) => RefreshFilteredCounts();
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

        RefreshRequiredExtensionsCache();
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

        RefreshRequiredExtensionsCache();
        RebuildFilteredItems();
    }

    partial void OnDoRecursiveSearchChanged(bool value)
    {
        ReloadCurrentFolder();
    }

    partial void OnShowFoldersChanged(bool value)
    {
        RebuildFilteredItems();
    }

    partial void OnShowHiddenFilesChanged(bool value)
    {
        ReloadCurrentFolder();
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
        FilteredFolderItems.Clear();

        if (_db == null)
        {
            return;
        }

        foreach (FolderBase folderBase in _db.Folders)
        {
            var folder = new Folder(folderBase.Path, folderBase.Name);
            CurrentFolderItems.Add(folder);
        }

        RebuildFilteredItems();
    }

    private void SetCurrentFolderItems(Folder newCurrentFolder)
    {
        _ = SetCurrentFolderItemsAsync(newCurrentFolder);
    }

    private async Task SetCurrentFolderItemsAsync(Folder newCurrentFolder)
    {
        if (newCurrentFolder.Name == "BaseFolders")
        {
            SetCurrentPathToHome();
            return;
        }

        _itemSearchCts?.Cancel();
        _itemSearchCts = new CancellationTokenSource();
        var cancellationToken = _itemSearchCts.Token;

        BeginSearchProgress();

        CurrentFolderItems.Clear();
        FilteredFolderItems.Clear();

        if (_itemSearchService.TryGetCachedAllItems(newCurrentFolder.Path, DoRecursiveSearch, ShowHiddenFiles, out var cachedItems))
        {
            foreach (var item in cachedItems)
            {
                CurrentFolderItems.Add(item);
                if (MatchesActiveFilters(item))
                {
                    FilteredFolderItems.Add(item);
                }
            }

            ScannedFolderCount = cachedItems.Count(item => item is Folder);
            ScannedFileCount = cachedItems.Count(item => item is File);
            ScannedDepth = CalculateCachedDepth(newCurrentFolder.Path, cachedItems);
            EstimatedSearchProgress = 100;
            IsSearching = false;

            return;
        }

        try
        {
            await _itemSearchService.SearchAsync(
                newCurrentFolder,
                includeSubdirectories: DoRecursiveSearch,
                includeHiddenFiles: ShowHiddenFiles,
                onItem: item =>
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        CurrentFolderItems.Add(item);
                        if (MatchesActiveFilters(item))
                        {
                            FilteredFolderItems.Add(item);
                        }
                    });
                },
                onProgress: (folders, files, depth, progress) =>
                {
                    App.Current.Dispatcher.Invoke(() =>
                    {
                        ScannedFolderCount = folders;
                        ScannedFileCount = files;
                        ScannedDepth = depth;
                        EstimatedSearchProgress = progress;
                    });
                },
                cancellationToken);

            EstimatedSearchProgress = 100;
            IsSearching = false;
        }
        catch (OperationCanceledException)
        {
            IsSearching = false;
        }
    }

    private void BeginSearchProgress()
    {
        IsSearching = true;
        ScannedFolderCount = 0;
        ScannedFileCount = 0;
        ScannedDepth = 0;
        EstimatedSearchProgress = 0;
    }

    private static int CalculateCachedDepth(string rootPath, IReadOnlyList<ExplorerItem> items)
    {
        var maxDepth = 0;

        foreach (var folder in items.OfType<Folder>())
        {
            maxDepth = Math.Max(maxDepth, GetDepth(rootPath, folder.Path));
        }

        return maxDepth;
    }

    private static int GetDepth(string rootPath, string currentPath)
    {
        if (currentPath.Length <= rootPath.Length)
            return 0;

        var relative = currentPath[rootPath.Length..]
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.IsNullOrWhiteSpace(relative))
            return 0;

        return relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries).Length;
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

    private static bool IsHiddenItem(ExplorerItem item)
    {
        try
        {
            if (item is Folder folder)
            {
                var attributes = System.IO.File.GetAttributes(folder.Path);
                return (attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
            }

            if (item is File file && !string.IsNullOrWhiteSpace(file.FullPath))
            {
                var attributes = System.IO.File.GetAttributes(file.FullPath);
                return (attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
            }
        }
        catch
        {
        }

        return false;
    }

    private static bool MatchesExtensionFilter(ExplorerItem item, HashSet<string> requiredExtensions)
    {
        if (requiredExtensions.Count == 0)
            return true;

        if (item is not File fileItem)
            return false;

        return requiredExtensions.Contains(fileItem.Extension);
    }

    private bool MatchesActiveFilters(ExplorerItem item)
    {
        if (!ShowFolders && item is Folder)
            return false;

        if (!ShowHiddenFiles && IsHiddenItem(item))
            return false;

        return MatchesExtensionFilter(item, _requiredExtensionsCache);
    }

    private void RebuildFilteredItems()
    {
        FilteredFolderItems.Clear();
        foreach (var item in CurrentFolderItems)
        {
            if (MatchesActiveFilters(item))
            {
                FilteredFolderItems.Add(item);
            }
        }

        RefreshFilteredCounts();
    }

    private void RefreshFilteredCounts()
    {
        FilteredFolderCount = FilteredFolderItems.Count(item => item is Folder);
        FilteredFileCount = FilteredFolderItems.Count(item => item is File);
    }

    private void RefreshRequiredExtensionsCache()
    {
        _requiredExtensionsCache.Clear();
        foreach (var filter in _requiredExtentionFilters)
        {
            // FileType extensions are already normalized in FileTypes.Types
            _requiredExtensionsCache.Add(filter.FileType.Extension);
        }
    }

    private void ReloadCurrentFolder()
    {
        if (Breadcrumbs.Count == 0)
        {
            return;
        }

        var currentFolder = Breadcrumbs.Last();
        if (currentFolder.Name == "BaseFolders")
        {
            SetCurrentPathToHome();
            return;
        }

        SetCurrentFolderItems(currentFolder);
    }
}