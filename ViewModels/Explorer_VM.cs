using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Threading;
using TagExplorer.Data;
using TagExplorer.Models;
using TagExplorer.Services;
using File = TagExplorer.Models.File;
using Folder = TagExplorer.Models.Folder;

namespace TagExplorer.ViewModels;

public partial class Explorer_VM : ObservableObject
{
    public const int HistoryLength = 20;
    private const int UiItemBatchSize = 200;
    private const int ProgressUpdateIntervalMs = 100;

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
    private CancellationTokenSource? _filterRebuildCts;
    private int _activeSearchRequestId;
    private int _activeFilterRebuildRequestId;
    private bool _currentItemsExcludeHidden;
    private readonly HashSet<string> _requiredExtensionsCache = new(StringComparer.OrdinalIgnoreCase);

    public Explorer_VM()
    {
        _db = App.AppHost?.Services.GetService<AppDbContext>();

        CurrentFolderItems = new ObservableCollection<ExplorerItem>();
        FilteredFolderItems = new ObservableCollection<ExplorerItem>();
        FilteredFolderItems.CollectionChanged += OnFilteredFolderItemsCollectionChanged;
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
        if (SelectedItem == null || Breadcrumbs.Count == 0)
        {
            return;
        }

        var selectedItem = SelectedItem;
        SelectedItem = null;

        if (selectedItem is Folder folder)
        {
            var currentBreadcrumb = Breadcrumbs[ Breadcrumbs.Count - 1 ];

            if (currentBreadcrumb.Name == "BaseFolders")
            {
                Breadcrumbs.Clear();
                Breadcrumbs.Add(folder);
            }
            else
            {
                Breadcrumbs.Add(new Folder(folder.Name, currentBreadcrumb));
            }

            Folder newCurrentFolder = Breadcrumbs[ Breadcrumbs.Count - 1 ];

            SetCurrentFolderItems(newCurrentFolder);
        }

        AddToHistory(Breadcrumbs.ToList());
    }

    private void SetCurrentPathToHome()
    {
        _currentItemsExcludeHidden = false;

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

        var requestId = Interlocked.Increment(ref _activeSearchRequestId);

        var previousSearchCts = _itemSearchCts;
        var currentSearchCts = new CancellationTokenSource();
        _itemSearchCts = currentSearchCts;

        previousSearchCts?.Cancel();
        previousSearchCts?.Dispose();

        var cancellationToken = currentSearchCts.Token;
        _currentItemsExcludeHidden = !ShowHiddenFiles;

        BeginSearchProgress();

        CurrentFolderItems.Clear();
        FilteredFolderItems.Clear();

        if (_itemSearchService.TryGetCachedAllItems(newCurrentFolder.Path, DoRecursiveSearch, ShowHiddenFiles, out var cachedItems))
        {
            if (!IsLatestSearchRequest(requestId) || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            for (var i = 0; i < cachedItems.Count; i++)
            {
                if (!IsLatestSearchRequest(requestId) || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var item = cachedItems[i];
                CurrentFolderItems.Add(item);
                if (MatchesActiveFilters(item))
                {
                    FilteredFolderItems.Add(item);
                }

                if ((i + 1) % UiItemBatchSize == 0)
                {
                    await Task.Yield();
                }
            }

            if (IsLatestSearchRequest(requestId) && !cancellationToken.IsCancellationRequested)
            {
                ScannedFolderCount = cachedItems.Count(item => item is Folder);
                ScannedFileCount = cachedItems.Count(item => item is File);
                ScannedDepth = CalculateCachedDepth(newCurrentFolder.Path, cachedItems);
                EstimatedSearchProgress = 100;
                IsSearching = false;
            }

            return;
        }

        try
        {
            var bufferedItems = new List<ExplorerItem>(UiItemBatchSize);
            var bufferedItemsLock = new object();
            var isFlushScheduled = 0;
            var lastProgressUpdateTick = Environment.TickCount64;

            ExplorerItem[] DrainBufferedItems()
            {
                lock (bufferedItemsLock)
                {
                    if (bufferedItems.Count == 0)
                    {
                        return [];
                    }

                    var items = bufferedItems.ToArray();
                    bufferedItems.Clear();
                    return items;
                }
            }

            void FlushBufferedItemsOnUi()
            {
                var itemsToAdd = DrainBufferedItems();
                if (itemsToAdd.Length == 0)
                {
                    return;
                }

                foreach (var item in itemsToAdd)
                {
                    CurrentFolderItems.Add(item);
                    if (MatchesActiveFilters(item))
                    {
                        FilteredFolderItems.Add(item);
                    }
                }
            }

            void ScheduleBufferedItemsFlush()
            {
                if (Interlocked.Exchange(ref isFlushScheduled, 1) == 1)
                {
                    return;
                }

                _ = App.Current.Dispatcher.InvokeAsync(() =>
                {
                    try
                    {
                        if (!IsLatestSearchRequest(requestId) || cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        FlushBufferedItemsOnUi();
                    }
                    finally
                    {
                        Interlocked.Exchange(ref isFlushScheduled, 0);

                        bool shouldReschedule;
                        lock (bufferedItemsLock)
                        {
                            shouldReschedule = bufferedItems.Count >= UiItemBatchSize;
                        }

                        if (shouldReschedule && IsLatestSearchRequest(requestId) && !cancellationToken.IsCancellationRequested)
                        {
                            ScheduleBufferedItemsFlush();
                        }
                    }
                });
            }

            await _itemSearchService.SearchAsync(
                newCurrentFolder,
                includeSubdirectories: DoRecursiveSearch,
                includeHiddenFiles: ShowHiddenFiles,
                onItem: item =>
                {
                    if (!IsLatestSearchRequest(requestId) || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var shouldFlush = false;
                    lock (bufferedItemsLock)
                    {
                        bufferedItems.Add(item);
                        shouldFlush = bufferedItems.Count == 1 || bufferedItems.Count >= UiItemBatchSize;
                    }

                    if (shouldFlush)
                    {
                        ScheduleBufferedItemsFlush();
                    }
                },
                onProgress: (folders, files, depth, progress) =>
                {
                    if (!IsLatestSearchRequest(requestId) || cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    var nowTick = Environment.TickCount64;
                    if (progress < 100d && nowTick - lastProgressUpdateTick < ProgressUpdateIntervalMs)
                    {
                        return;
                    }

                    lastProgressUpdateTick = nowTick;

                    _ = App.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (!IsLatestSearchRequest(requestId) || cancellationToken.IsCancellationRequested)
                        {
                            return;
                        }

                        ScannedFolderCount = folders;
                        ScannedFileCount = files;
                        ScannedDepth = depth;
                        EstimatedSearchProgress = progress;
                    });
                },
                cancellationToken);

            await App.Current.Dispatcher.InvokeAsync(() =>
            {
                if (!IsLatestSearchRequest(requestId) || cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                FlushBufferedItemsOnUi();
            });

            if (IsLatestSearchRequest(requestId) && !cancellationToken.IsCancellationRequested)
            {
                EstimatedSearchProgress = 100;
                IsSearching = false;
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            if (IsLatestSearchRequest(requestId))
            {
                IsSearching = false;
            }
        }
    }

    private bool IsLatestSearchRequest(int requestId)
    {
        return requestId == Volatile.Read(ref _activeSearchRequestId);
    }

    private void OnFilteredFolderItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        switch (e.Action)
        {
            case NotifyCollectionChangedAction.Add:
                if (e.NewItems is not null)
                {
                    foreach (ExplorerItem item in e.NewItems)
                    {
                        if (item is Folder) FilteredFolderCount++;
                        else if (item is File) FilteredFileCount++;
                    }
                }
                break;

            case NotifyCollectionChangedAction.Remove:
                if (e.OldItems is not null)
                {
                    foreach (ExplorerItem item in e.OldItems)
                    {
                        if (item is Folder) FilteredFolderCount = Math.Max(0, FilteredFolderCount - 1);
                        else if (item is File) FilteredFileCount = Math.Max(0, FilteredFileCount - 1);
                    }
                }
                break;

            case NotifyCollectionChangedAction.Replace:
                if (e.OldItems is not null)
                {
                    foreach (ExplorerItem item in e.OldItems)
                    {
                        if (item is Folder) FilteredFolderCount = Math.Max(0, FilteredFolderCount - 1);
                        else if (item is File) FilteredFileCount = Math.Max(0, FilteredFileCount - 1);
                    }
                }

                if (e.NewItems is not null)
                {
                    foreach (ExplorerItem item in e.NewItems)
                    {
                        if (item is Folder) FilteredFolderCount++;
                        else if (item is File) FilteredFileCount++;
                    }
                }
                break;

            case NotifyCollectionChangedAction.Reset:
                FilteredFolderCount = 0;
                FilteredFileCount = 0;
                break;

            case NotifyCollectionChangedAction.Move:
            default:
                break;
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

        if (!ShowHiddenFiles && !_currentItemsExcludeHidden && IsHiddenItem(item))
            return false;

        return MatchesExtensionFilter(item, _requiredExtensionsCache);
    }

    private void RebuildFilteredItems()
    {
        _ = RebuildFilteredItemsAsync();
    }

    private async Task RebuildFilteredItemsAsync()
    {
        var requestId = Interlocked.Increment(ref _activeFilterRebuildRequestId);

        var previousCts = _filterRebuildCts;
        var currentCts = new CancellationTokenSource();
        _filterRebuildCts = currentCts;

        previousCts?.Cancel();
        previousCts?.Dispose();

        var cancellationToken = currentCts.Token;
        var snapshot = CurrentFolderItems.ToList();

        FilteredFolderItems.Clear();

        for (var i = 0; i < snapshot.Count; i++)
        {
            if (requestId != Volatile.Read(ref _activeFilterRebuildRequestId) || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var item = snapshot[i];
            if (MatchesActiveFilters(item))
            {
                FilteredFolderItems.Add(item);
            }

            if ((i + 1) % UiItemBatchSize == 0)
            {
                await Task.Yield();
            }
        }
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

        var currentFolder = Breadcrumbs[Breadcrumbs.Count - 1];
        if (currentFolder.Name == "BaseFolders")
        {
            SetCurrentPathToHome();
            return;
        }

        SetCurrentFolderItems(currentFolder);
    }
}