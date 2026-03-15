using System.Collections.Concurrent;
using System.IO;
using TagExplorer.Models;
using File = TagExplorer.Models.File;
using Folder = TagExplorer.Models.Folder;

namespace TagExplorer.Services;

public sealed class ItemSearchService
{
    private readonly ConcurrentDictionary<string, List<ExplorerItem>> _cache = new(StringComparer.OrdinalIgnoreCase);

    private static string GetCacheKey(string rootPath, bool includeSubdirectories, bool includeHiddenFiles)
        => $"{rootPath}|r:{includeSubdirectories}|h:{includeHiddenFiles}";

    public bool TryGetCachedAllItems(string rootPath, bool includeSubdirectories, bool includeHiddenFiles, out IReadOnlyList<ExplorerItem> items)
    {
        var key = GetCacheKey(rootPath, includeSubdirectories, includeHiddenFiles);
        if (_cache.TryGetValue(key, out var cached))
        {
            items = cached;
            return true;
        }

        items = Array.Empty<ExplorerItem>();
        return false;
    }

    public void Invalidate(string rootPath) => _cache.TryRemove(rootPath, out _);

    public async Task SearchAsync(
        Folder rootFolder,
        bool includeSubdirectories,
        bool includeHiddenFiles,
        Action<ExplorerItem> onItem,
        Action<int, int, int, double>? onProgress = null,
        CancellationToken cancellationToken = default)
    {
        var allItems = new List<ExplorerItem>();

        await Task.Run(() =>
        {
            foreach (var item in EnumerateItems(rootFolder, includeSubdirectories, includeHiddenFiles, onProgress, cancellationToken))
            {
                cancellationToken.ThrowIfCancellationRequested();
                allItems.Add(item);
                onItem(item);
            }
        }, cancellationToken);

        _cache[GetCacheKey(rootFolder.Path, includeSubdirectories, includeHiddenFiles)] = allItems;
    }

    private static IEnumerable<ExplorerItem> EnumerateItems(
        Folder rootFolder,
        bool includeSubdirectories,
        bool includeHiddenFiles,
        Action<int, int, int, double>? onProgress,
        CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(rootFolder.Path);
        var scannedFolderCount = 0;
        var scannedFileCount = 0;
        var maxScannedDepth = 0;
        var maxEstimatedProgress = 0d;

        static double ClampProgress(double progress)
            => Math.Clamp(progress, 0d, 100d);

        void ReportProgress()
        {
            var estimatedProgress = scannedFolderCount == 0
                ? 0d
                : ClampProgress((double)scannedFolderCount / (scannedFolderCount + pending.Count) * 100d);

            if (estimatedProgress < maxEstimatedProgress)
            {
                estimatedProgress = maxEstimatedProgress;
            }
            else
            {
                maxEstimatedProgress = estimatedProgress;
            }

            onProgress?.Invoke(scannedFolderCount, scannedFileCount, maxScannedDepth, estimatedProgress);
        }

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var currentPath = pending.Pop();
            scannedFolderCount++;
            maxScannedDepth = Math.Max(maxScannedDepth, GetDepth(rootFolder.Path, currentPath));
            ReportProgress();

            IEnumerable<string> subDirectories;
            try
            {
                subDirectories = Directory.EnumerateDirectories(currentPath);
            }
            catch
            {
                subDirectories = Enumerable.Empty<string>();
            }

            foreach (var subDirectory in subDirectories)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!includeHiddenFiles && IsHidden(subDirectory))
                {
                    continue;
                }

                var folderItem = new Folder(subDirectory, Path.GetFileName(subDirectory));
                yield return folderItem;

                if (includeSubdirectories)
                {
                    pending.Push(subDirectory);
                }
            }

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(currentPath);
            }
            catch
            {
                files = Enumerable.Empty<string>();
            }

            foreach (var filePath in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!includeHiddenFiles && IsHidden(filePath))
                {
                    continue;
                }

                scannedFileCount++;
                ReportProgress();
                yield return new File(Path.GetFileName(filePath), Path.GetExtension(filePath), filePath);
            }

            if (!includeSubdirectories)
            {
                break;
            }
        }

        onProgress?.Invoke(scannedFolderCount, scannedFileCount, maxScannedDepth, 100d);
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
}