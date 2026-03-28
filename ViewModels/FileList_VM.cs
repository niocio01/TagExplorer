using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using TagExplorer.Data;
using TagExplorer.Models;
using TagExplorer.Services;
using File = TagExplorer.Models.File;
using Folder = TagExplorer.Models.Folder;

namespace TagExplorer.ViewModels;

public partial class FileList_VM : ObservableObject
{
    [ObservableProperty]
    private ObservableCollection<ExplorerItem> _currentFolderItems;

    [ObservableProperty]
    private ObservableCollection<ExplorerItem> _filteredFolderItems;

    [ObservableProperty]
    private ExplorerItem? _selectedItem;

    [ObservableProperty]
    private bool _isSearching;

    private bool _isCacheBuilding;

    public bool IsCacheBuilding
    {
        get => _isCacheBuilding;
        private set => SetProperty(ref _isCacheBuilding, value);
    }

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

    private bool _currentItemsExcludeHidden;
    private bool _doRecursiveSearch = true;
    private bool _showFolders = true;
    private bool _showHiddenFiles;
    private readonly HashSet<string> _requiredExtensionsCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<int> _requiredTagIdsCache = [];
    private readonly HashSet<int> _disallowedTagIdsCache = [];
    private readonly TagAssignmentService? _tagAssignmentService;
    private readonly ItemSearchService? _itemSearchService;
    private readonly DataCachingService? _dataCachingService;
    private IReadOnlyDictionary<int, CompactTagDefinition> _compactTagDefinitionsById =
        new Dictionary<int, CompactTagDefinition>();
    private List<BaseFolder> _homeBaseFolders = [];
    private Folder? _currentFolder;
    private CancellationTokenSource? _filterCancellationTokenSource;

    public string? CurrentFolderPath => _currentFolder?.Path;

    public event EventHandler<Folder>? FolderDoubleClicked;
    public event EventHandler<ExplorerItem?>? SelectedItemChanged;

    private FileListFilterOptions FileListFilterOptions => new(
        _showFolders,
        IncludeSubdirectoriesForSearch,
        _currentFolder?.Path,
        _showHiddenFiles,
        _currentItemsExcludeHidden,
        _requiredExtensionsCache,
        _requiredTagIdsCache,
        _disallowedTagIdsCache);

    private bool HasActiveFileFilters => _requiredExtensionsCache.Count > 0;
    private bool HasActiveTagFilters => _requiredTagIdsCache.Count > 0 || _disallowedTagIdsCache.Count > 0;

    private bool IncludeSubdirectoriesForSearch => _doRecursiveSearch && (HasActiveFileFilters || HasActiveTagFilters);

    public FileList_VM(
        TagAssignmentService? tagAssignmentService = null,
        ItemSearchService? itemSearchService = null,
        DataCachingService? dataCachingService = null)
    {
        _tagAssignmentService = tagAssignmentService;
        _itemSearchService = itemSearchService;
        _dataCachingService = dataCachingService;
        CurrentFolderItems = new ObservableCollection<ExplorerItem>();
        FilteredFolderItems = new ObservableCollection<ExplorerItem>();
        FilteredFolderItems.CollectionChanged += OnFilteredFolderItemsCollectionChanged;

        if (_itemSearchService is not null)
        {
            ApplyCacheBuildProgress(_itemSearchService.LastCacheBuildProgress);
            _itemSearchService.CacheBuildProgressChanged += OnCacheBuildProgressChanged;
        }
    }

    public void UpdateFilters(
        bool doRecursiveSearch,
        bool showFolders,
        bool showHiddenFiles,
        IEnumerable<string> requiredExtensions,
        IEnumerable<int> requiredTagIds,
        IEnumerable<int> disallowedTagIds,
        bool reloadCurrentFolder)
    {
        _doRecursiveSearch = doRecursiveSearch;
        _showFolders = showFolders;
        _showHiddenFiles = showHiddenFiles;

        FileList.RebuildRequiredExtensions(_requiredExtensionsCache, requiredExtensions);
        RebuildRequiredTagIds(_requiredTagIdsCache, requiredTagIds);
        RebuildRequiredTagIds(_disallowedTagIdsCache, disallowedTagIds);

        if (reloadCurrentFolder && _currentFolder is not null)
        {
            _ = SetCurrentFolderItemsAsync(_currentFolder);
            return;
        }

        RebuildFilteredItems();
    }

    public void SetHomeItems(IEnumerable<BaseFolder> folderBases)
    {
        _homeBaseFolders = [.. folderBases];
        _currentFolder = null;
        OnPropertyChanged(nameof(CurrentFolderPath));
        _currentItemsExcludeHidden = false;

        CurrentFolderItems.Clear();
        FilteredFolderItems.Clear();

        foreach (var folderBase in _homeBaseFolders)
        {
            var folder = new Folder(folderBase.Path, folderBase.Name, false);
            FileList.AddItem(folder, CurrentFolderItems, FilteredFolderItems, FileListFilterOptions);
        }

        _compactTagDefinitionsById = new Dictionary<int, CompactTagDefinition>();
        RebuildFilteredItems();
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

    partial void OnSelectedItemChanged(ExplorerItem? value)
    {
        SelectedItemChanged?.Invoke(this, value);
    }

    private async Task SetCurrentFolderItemsAsync(Folder newCurrentFolder)
    {
        _currentFolder = newCurrentFolder;
        OnPropertyChanged(nameof(CurrentFolderPath));
        _currentItemsExcludeHidden = !_showHiddenFiles;

        CurrentFolderItems.Clear();
        FilteredFolderItems.Clear();

        foreach (var child in newCurrentFolder.Children)
        {
            CurrentFolderItems.Add(child);
        }

        await RebuildFilteredItemsAsync();
    }

    private void OnCacheBuildProgressChanged(ItemCacheBuildProgress progress)
    {
        var dispatcher = Application.Current?.Dispatcher;

        if (dispatcher is not null && !dispatcher.CheckAccess())
        {
            _ = dispatcher.InvokeAsync(() => ApplyCacheBuildProgress(progress));
            return;
        }

        ApplyCacheBuildProgress(progress);
    }

    private void ApplyCacheBuildProgress(in ItemCacheBuildProgress progress)
    {
        ScannedFolderCount = progress.ScannedFolderCount;
        ScannedFileCount = progress.ScannedFileCount;
        ScannedDepth = progress.ScannedDepth;
        IsCacheBuilding = !progress.IsCompleted;
        EstimatedSearchProgress = progress.IsCompleted ? 100 : 0;
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

    private void RebuildFilteredItems()
    {
        _ = RebuildFilteredItemsAsync();
    }

    private async Task RebuildFilteredItemsAsync()
    {
        _filterCancellationTokenSource?.Cancel();
        _filterCancellationTokenSource?.Dispose();
        _filterCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _filterCancellationTokenSource.Token;

        IsSearching = true;

        try
        {
            var filteredItems = await BuildFilteredItemsAsync(cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();
            FilteredFolderItems.Clear();
            foreach (var item in filteredItems)
            {
                FilteredFolderItems.Add(item);
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            if (_filterCancellationTokenSource?.Token == cancellationToken)
            {
                IsSearching = false;
            }
        }
    }

    private async Task<IReadOnlyList<ExplorerItem>> BuildFilteredItemsAsync(CancellationToken cancellationToken)
    {
        if (_currentFolder is null || _itemSearchService is null)
        {
            return BuildFilteredItemsFromCurrentFolder(cancellationToken);
        }

        var filterCriteria = new FilterCriteria(
            _currentFolder.Path,
            _requiredTagIdsCache,
            _disallowedTagIdsCache,
            _requiredExtensionsCache,
            IncludeHidden: true,
            IncludeSubItems: IncludeSubdirectoriesForSearch);

        var candidates = await _itemSearchService.GetFilteredItemsAsync(filterCriteria, cancellationToken);
        return ScopeToCurrentFolder(candidates, _currentFolder, IncludeSubdirectoriesForSearch, _showFolders, cancellationToken);
    }

    private IReadOnlyList<ExplorerItem> BuildFilteredItemsFromCurrentFolder(CancellationToken cancellationToken)
    {
        var result = new List<ExplorerItem>(CurrentFolderItems.Count);
        foreach (var item in CurrentFolderItems)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (FileList.Matches(item, FileListFilterOptions))
            {
                result.Add(item);
            }
        }

        return result;
    }

    private static IReadOnlyList<ExplorerItem> ScopeToCurrentFolder(
        IReadOnlyList<ExplorerItem> source,
        Folder currentFolder,
        bool includeSubItems,
        bool includeFolders,
        CancellationToken cancellationToken)
    {
        var currentPath = currentFolder.Path;
        var pathPrefix = currentPath + Path.DirectorySeparatorChar;
        var result = new List<ExplorerItem>(source.Count);

        foreach (var item in source)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!includeFolders && item is Folder)
            {
                continue;
            }

            if (includeSubItems)
            {
                if (item.Path.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    result.Add(item);
                }

                continue;
            }

            if (item.ParentFolder is null)
            {
                continue;
            }

            if (string.Equals(item.ParentFolder.Path, currentPath, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(item);
            }
        }

        return result;
    }

    public IReadOnlyList<CompactTagToken> GetCompactTagTokens(ExplorerItem? item, int maxVisible = 3)
    {
        if (item is null || maxVisible <= 0)
        {
            return [];
        }

        var tagIds = _dataCachingService.GetTagApplicationsForPath(item.Path).Select(x => x.Tag.Id.Value);

        var orderedDefinitions = tagIds
            .Select(id => _compactTagDefinitionsById.TryGetValue(id, out var definition)
                ? definition
                : new CompactTagDefinition { Id = id, Name = id.ToString() })
            .OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var visible = orderedDefinitions
            .Take(maxVisible)
            .Select(definition => new CompactTagToken
            {
                Text = BuildCompactText(definition),
                ColorHex = string.IsNullOrWhiteSpace(definition.ColorHex) ? "#FF9E9E9E" : definition.ColorHex,
                Tooltip = definition.Name,
                IsOverflow = false
            })
            .ToList();

        var overflowCount = orderedDefinitions.Count - visible.Count;
        if (overflowCount > 0)
        {
            visible.Add(new CompactTagToken
            {
                Text = $"+{overflowCount}",
                ColorHex = null,
                Tooltip = $"{overflowCount} more tags",
                IsOverflow = true
            });
        }

        return visible;
    }

    private static string BuildCompactText(CompactTagDefinition definition)
    {
        if (!string.IsNullOrWhiteSpace(definition.ShortCode))
        {
            return definition.ShortCode.Trim().ToUpperInvariant();
        }

        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            return "?";
        }

        var letters = new string(definition.Name
            .Where(char.IsLetterOrDigit)
            .ToArray());

        if (letters.Length == 0)
        {
            return "?";
        }

        return letters.Length == 1
            ? letters[..1].ToUpperInvariant()
            : letters[..2].ToUpperInvariant();
    }

    private static void RebuildRequiredTagIds(HashSet<int> target, IEnumerable<int> source)
    {
        target.Clear();

        foreach (var tagId in source)
        {
            target.Add(tagId);
        }
    }

}
