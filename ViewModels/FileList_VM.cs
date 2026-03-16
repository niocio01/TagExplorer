using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading;
using TagExplorer.Data;
using TagExplorer.Models;
using TagExplorer.Services;
using File = TagExplorer.Models.File;
using Folder = TagExplorer.Models.Folder;

namespace TagExplorer.ViewModels;

public partial class FileList_VM : ObservableObject
{
    private const int UiItemBatchSize = 200;
    private const int ProgressUpdateIntervalMs = 100;

    [ObservableProperty]
    private ObservableCollection<ExplorerItem> _currentFolderItems;

    [ObservableProperty]
    private ObservableCollection<ExplorerItem> _filteredFolderItems;

    [ObservableProperty]
    private ExplorerItem? _selectedItem;

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

    private readonly ItemSearchService _itemSearchService = new();
    private CancellationTokenSource? _itemSearchCts;
    private CancellationTokenSource? _filterRebuildCts;
    private int _activeSearchRequestId;
    private int _activeFilterRebuildRequestId;
    private bool _currentItemsExcludeHidden;
    private bool _doRecursiveSearch = true;
    private bool _showFolders = true;
    private bool _showHiddenFiles;
    private readonly HashSet<string> _requiredExtensionsCache = new(StringComparer.OrdinalIgnoreCase);
    private Folder? _currentFolder;

    public string? CurrentFolderPath => _currentFolder?.Path;

    public event EventHandler<Folder>? FolderDoubleClicked;

    private FileListFilterOptions FileListFilterOptions => new(
        _showFolders,
        IncludeSubdirectoriesForSearch,
        _currentFolder?.Path,
        _showHiddenFiles,
        _currentItemsExcludeHidden,
        _requiredExtensionsCache);

    private bool HasActiveFileFilters => _requiredExtensionsCache.Count > 0;

    private bool IncludeSubdirectoriesForSearch => _doRecursiveSearch && HasActiveFileFilters;

    public FileList_VM()
    {
        CurrentFolderItems = new ObservableCollection<ExplorerItem>();
        FilteredFolderItems = new ObservableCollection<ExplorerItem>();
        FilteredFolderItems.CollectionChanged += OnFilteredFolderItemsCollectionChanged;
    }

    public void UpdateFilters(
        bool doRecursiveSearch,
        bool showFolders,
        bool showHiddenFiles,
        IEnumerable<string> requiredExtensions,
        bool reloadCurrentFolder)
    {
        var previousIncludeSubdirectoriesForSearch = IncludeSubdirectoriesForSearch;

        _doRecursiveSearch = doRecursiveSearch;
        _showFolders = showFolders;
        _showHiddenFiles = showHiddenFiles;

        FileList.RebuildRequiredExtensions(_requiredExtensionsCache, requiredExtensions);

        var includeSubdirectoriesForSearchChanged = previousIncludeSubdirectoriesForSearch != IncludeSubdirectoriesForSearch;

        if ((reloadCurrentFolder || includeSubdirectoriesForSearchChanged)
            && _currentFolder is not null
            && _currentFolder.Name != "BaseFolders")
        {
            SetCurrentFolderItems(_currentFolder);
            return;
        }

        RebuildFilteredItems();
    }

    public void SetHomeItems(IEnumerable<FolderBase> folderBases)
    {
        _currentFolder = null;
        OnPropertyChanged(nameof(CurrentFolderPath));
        _currentItemsExcludeHidden = false;

        CurrentFolderItems.Clear();
        FilteredFolderItems.Clear();

        foreach (var folderBase in folderBases)
        {
            var folder = new Folder(folderBase.Path, folderBase.Name);
            FileList.AddItem(folder, CurrentFolderItems, FilteredFolderItems, FileListFilterOptions);
        }
    }

    public void SetCurrentFolderItems(Folder newCurrentFolder)
    {
        _ = SetCurrentFolderItemsAsync(newCurrentFolder);
    }

    public void HandleItemDoubleClick()
    {
        if (SelectedItem is not Folder folder)
        {
            return;
        }

        SelectedItem = null;
        FolderDoubleClicked?.Invoke(this, folder);
    }

    private async Task SetCurrentFolderItemsAsync(Folder newCurrentFolder)
    {
        _currentFolder = newCurrentFolder;
        OnPropertyChanged(nameof(CurrentFolderPath));

        var requestId = Interlocked.Increment(ref _activeSearchRequestId);

        var previousSearchCts = _itemSearchCts;
        var currentSearchCts = new CancellationTokenSource();
        _itemSearchCts = currentSearchCts;

        previousSearchCts?.Cancel();
        previousSearchCts?.Dispose();

        var cancellationToken = currentSearchCts.Token;
        var includeSubdirectoriesForSearch = IncludeSubdirectoriesForSearch;
        _currentItemsExcludeHidden = !_showHiddenFiles;

        BeginSearchProgress();

        CurrentFolderItems.Clear();
        FilteredFolderItems.Clear();

        if (_itemSearchService.TryGetCachedAllItems(newCurrentFolder.Path, includeSubdirectoriesForSearch, _showHiddenFiles, out var cachedItems))
        {
            if (!IsLatestSearchRequest(requestId) || cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var cachedPopulationCompleted = await FileList.PopulateCurrentAndFilteredAsync(
                cachedItems,
                CurrentFolderItems,
                FilteredFolderItems,
                FileListFilterOptions,
                UiItemBatchSize,
                () => IsLatestSearchRequest(requestId),
                cancellationToken);

            if (!cachedPopulationCompleted)
            {
                return;
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

                FileList.AddItems(itemsToAdd, CurrentFolderItems, FilteredFolderItems, FileListFilterOptions);
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
                includeSubdirectories: includeSubdirectoriesForSearch,
                includeHiddenFiles: _showHiddenFiles,
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

    private async void RebuildFilteredItems()
    {
        var requestId = Interlocked.Increment(ref _activeFilterRebuildRequestId);

        var previousCts = _filterRebuildCts;
        var currentCts = new CancellationTokenSource();
        _filterRebuildCts = currentCts;

        previousCts?.Cancel();
        previousCts?.Dispose();

        var cancellationToken = currentCts.Token;
        var snapshot = CurrentFolderItems.ToList();

        await FileList.RebuildFilteredItemsAsync(
            snapshot,
            FilteredFolderItems,
            FileListFilterOptions,
            UiItemBatchSize,
            cancellationToken);

        if (requestId != Volatile.Read(ref _activeFilterRebuildRequestId) || cancellationToken.IsCancellationRequested)
        {
            return;
        }
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
}
