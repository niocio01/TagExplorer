using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Threading.Channels;
using TagExplorer.Data;
using TagExplorer.Models;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;
using File = TagExplorer.Models.File;
using Folder = TagExplorer.Models.Folder;

namespace TagExplorer.Services;

public readonly record struct ItemCacheBuildProgress(
    int ScannedFolderCount,
    int ScannedFileCount,
    int ScannedDepth,
    bool IsCompleted);

public class ItemSearchService
{
    private readonly DataCachingService _dataCachingService;
    private readonly ILogger<ItemSearchService>? _logger;
    private IReadOnlyList<ExplorerItem> _cachedItems = Array.Empty<ExplorerItem>();
    private ItemCacheBuildProgress _lastCacheBuildProgress = new(0, 0, 0, true);

    public ItemCacheBuildProgress LastCacheBuildProgress => _lastCacheBuildProgress;
    public event Action<ItemCacheBuildProgress>? CacheBuildProgressChanged;

    public ItemSearchService(DataCachingService dataCachingService, ILogger<ItemSearchService>? logger = null)
    {
        _dataCachingService = dataCachingService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ExplorerItem>> BuildCacheAndIndexesAsync(
        IReadOnlyList<BaseFolder> baseFolders,
        IReadOnlyList<TagAssignment> tagAssignments,
        int maxDegreeOfParallelism = 0,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        ReportCacheBuildProgress(new ItemCacheBuildProgress(0, 0, 0, false));

        _logger?.LogInformation(
            "Cache build started. RequestedBaseFolders={RequestedBaseFolders}, MaxDegreeOfParallelism={MaxDegreeOfParallelism}",
            baseFolders.Count,
            maxDegreeOfParallelism);

        if (baseFolders.Count == 0)
        {
            Clear();
            stopwatch.Stop();
            _logger?.LogInformation("Cache build finished. No base folders configured. DurationMs={DurationMs}", stopwatch.ElapsedMilliseconds);
            return _cachedItems;
        }

        var roots = new List<Folder>(baseFolders.Count);
        foreach (var baseFolder in baseFolders)
        {
            if (string.IsNullOrWhiteSpace(baseFolder.Path) || !Directory.Exists(baseFolder.Path))
            {
                continue;
            }

            roots.Add(new Folder(baseFolder.Path, baseFolder.Name, false));
        }

        if (roots.Count == 0)
        {
            Clear();
            stopwatch.Stop();
            _logger?.LogInformation("Cache build finished. No valid root folders found. DurationMs={DurationMs}", stopwatch.ElapsedMilliseconds);
            return _cachedItems;
        }

        var workerCount = maxDegreeOfParallelism > 0
            ? maxDegreeOfParallelism
            : Math.Min(Environment.ProcessorCount, 6);

        _logger?.LogInformation(
            "Cache build scanning initialized. ValidRoots={ValidRoots}, WorkerCount={WorkerCount}",
            roots.Count,
            workerCount);

        var folderChannel = Channel.CreateUnbounded<Folder>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = false
        });

        var itemChannel = Channel.CreateUnbounded<ExplorerItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _dataCachingService.StartBuild();
        var cachedItems = new List<ExplorerItem>();
        var scannedFolderCount = 0;
        var scannedFileCount = 0;
        var scannedDepth = 0;

        var indexerTask = Task.Run(async () =>
        {
            await foreach (var item in itemChannel.Reader.ReadAllAsync(cancellationToken))
            {
                cachedItems.Add(item);
                _dataCachingService.AddItem(item);

                switch (item)
                {
                    case Folder folder:
                        scannedFolderCount++;
                        scannedDepth = Math.Max(scannedDepth, GetDepth(folder));
                        break;
                    case File file:
                        scannedFileCount++;
                        scannedDepth = Math.Max(scannedDepth, GetDepth(file));
                        break;
                }

                var processedCount = scannedFolderCount + scannedFileCount;
                if (processedCount == 1 || processedCount % 200 == 0)
                {
                    ReportCacheBuildProgress(new ItemCacheBuildProgress(
                        scannedFolderCount,
                        scannedFileCount,
                        scannedDepth,
                        false));
                }
            }
        }, cancellationToken);

        var pendingFolders = roots.Count;

        foreach (var root in roots)
        {
            await itemChannel.Writer.WriteAsync(root, cancellationToken);
            await folderChannel.Writer.WriteAsync(root, cancellationToken);
        }

        var workers = Enumerable.Range(0, workerCount)
            .Select(_ => Task.Run(async () =>
            {
                while (await folderChannel.Reader.WaitToReadAsync(cancellationToken))
                {
                    while (folderChannel.Reader.TryRead(out var folder))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        IEnumerable<string> subDirectories;
                        try
                        {
                            subDirectories = Directory.EnumerateDirectories(folder.Path);
                        }
                        catch
                        {
                            subDirectories = Enumerable.Empty<string>();
                        }

                        foreach (var subDirectoryPath in subDirectories)
                        {
                            var subFolder = new Folder(Path.GetFileName(subDirectoryPath), folder, false);
                            folder.Children.Add(subFolder);

                            Interlocked.Increment(ref pendingFolders);
                            await itemChannel.Writer.WriteAsync(subFolder, cancellationToken);
                            await folderChannel.Writer.WriteAsync(subFolder, cancellationToken);
                        }

                        IEnumerable<string> files;
                        try
                        {
                            files = Directory.EnumerateFiles(folder.Path);
                        }
                        catch
                        {
                            files = Enumerable.Empty<string>();
                        }

                        foreach (var filePath in files)
                        {
                            var file = new File(Path.GetFileName(filePath), folder, Path.GetExtension(filePath), filePath, false);
                            folder.Children.Add(file);
                            await itemChannel.Writer.WriteAsync(file, cancellationToken);
                        }

                        if (Interlocked.Decrement(ref pendingFolders) == 0)
                        {
                            folderChannel.Writer.TryComplete();
                        }
                    }
                }
            }, cancellationToken))
            .ToArray();

        await Task.WhenAll(workers);
        itemChannel.Writer.TryComplete();
        await indexerTask;
        _dataCachingService.BuildTagIndex();

        ReportCacheBuildProgress(new ItemCacheBuildProgress(
            scannedFolderCount,
            scannedFileCount,
            scannedDepth,
            true));

        _cachedItems = cachedItems;
        stopwatch.Stop();
        _logger?.LogInformation(
            "Cache build finished. DurationMs={DurationMs}, Items={Items}, Folders={Folders}, Files={Files}, Depth={Depth}",
            stopwatch.ElapsedMilliseconds,
            scannedFolderCount + scannedFileCount,
            scannedFolderCount,
            scannedFileCount,
            scannedDepth);
        return _cachedItems;
    }

    
    public async Task<IReadOnlyList<ExplorerItem>> GetFilteredItemsAsync(
        FilterCriteria filterCriteria,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return await Task.Run(() =>
        {
            var candidates = _cachedItems;

            candidates = _dataCachingService.GetCandidates(filterCriteria.CurrentFolderPath, filterCriteria.requiredTagIds, filterCriteria.disallowedTagIds, filterCriteria.requiredExtensions);

            // candidates = FilterByHiddenItems(candidates, filterCriteria.IncludeHidden, cancellationToken);
            // candidates = FilterBySubItems(candidates, filterCriteria.IncludeSubItems, cancellationToken);

            return candidates;
        }, cancellationToken);
    }

    public bool TryGetItemByPath(string path, out ExplorerItem? item)
    {
        item = null;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }
        item = _cachedItems.FirstOrDefault(item => string.Equals(item.Path, path, StringComparison.OrdinalIgnoreCase));
        if (item == null) 
            return false;
        return true;
    }

    private static IReadOnlyList<ExplorerItem> FilterByHiddenItems(
        IReadOnlyList<ExplorerItem> source,
        bool includeHidden,
        CancellationToken cancellationToken)
    {
        if (includeHidden || source.Count == 0)
        {
            return source;
        }

        var result = new List<ExplorerItem>(source.Count);
        for (var i = 0; i < source.Count; i++)
        {
            if ((i & 255) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var item = source[i];
            if (!IsHiddenItem(item))
            {
                result.Add(item);
            }
        }

        return result;
    }

    private static IReadOnlyList<ExplorerItem> FilterBySubItems(
        IReadOnlyList<ExplorerItem> source,
        bool includeSubItems,
        CancellationToken cancellationToken)
    {
        if (includeSubItems || source.Count == 0)
        {
            return source;
        }

        var result = new List<ExplorerItem>(source.Count);
        for (var i = 0; i < source.Count; i++)
        {
            if ((i & 255) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var item = source[i];
            if (item.ParentFolder is null)
            {
                result.Add(item);
            }
        }

        return result;
    }

    private static bool IsHiddenItem(ExplorerItem item)
    {
        return item switch
        {
            Folder folder => folder.IsHidden == true || IsHidden(folder.Path),
            File file => file.IsHidden == true || (!string.IsNullOrWhiteSpace(file.FullPath) && IsHidden(file.FullPath)),
            _ => false
        };
    }

    private static bool IsHidden(string path)
    {
        try
        {
            var attributes = System.IO.File.GetAttributes(path);
            return (attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
        }
        catch
        {
            return false;
        }
    }

    public void Clear()
    {
        _cachedItems = Array.Empty<ExplorerItem>();
        _dataCachingService.Clear();
        ReportCacheBuildProgress(new ItemCacheBuildProgress(0, 0, 0, true));
    }

    private void ReportCacheBuildProgress(in ItemCacheBuildProgress progress)
    {
        _lastCacheBuildProgress = progress;
        CacheBuildProgressChanged?.Invoke(progress);
    }

    private static int GetDepth(ExplorerItem item)
    {
        var depth = 0;
        var parent = item.ParentFolder;

        while (parent is not null)
        {
            depth++;
            parent = parent.ParentFolder;
        }

        return depth;
    }

    
}

public readonly record struct FilterCriteria(
    string? CurrentFolderPath,
    IReadOnlySet<int> requiredTagIds,
    IReadOnlySet<int> disallowedTagIds,
    IReadOnlySet<string> requiredExtensions,
    bool IncludeHidden = true,
    bool IncludeSubItems = true,
    bool IncludeVirtualTagedItems = true,
    DateTime? CreatedRangeStart = null,
    DateTime? CreatedRangeEnd = null,
    DateTime? ModifiedRangeStart = null,
    DateTime? ModifiedRangeEnd = null);

