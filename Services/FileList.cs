using System.Collections.ObjectModel;
using System.IO;
using TagExplorer.Models;
using File = TagExplorer.Models.File;
using Folder = TagExplorer.Models.Folder;

namespace TagExplorer.Services;

public readonly record struct FileListFilterOptions(
    bool ShowFolders,
    bool IncludeSubdirectories,
    string? CurrentFolderPath,
    bool ShowHiddenFiles,
    bool CurrentItemsExcludeHidden,
    IReadOnlySet<string> RequiredExtensions,
    IReadOnlySet<int> RequiredTagIds,
    IReadOnlySet<int> DisallowedTagIds,
    Func<ExplorerItem, bool>? MatchesTagFilter);

public static class FileList
{
    public static void AddItem(
        ExplorerItem item,
        ObservableCollection<ExplorerItem> currentItems,
        ObservableCollection<ExplorerItem> filteredItems,
        in FileListFilterOptions options)
    {
        currentItems.Add(item);
        if (Matches(item, options))
        {
            filteredItems.Add(item);
        }
    }

    public static void AddItems(
        IReadOnlyList<ExplorerItem> items,
        ObservableCollection<ExplorerItem> currentItems,
        ObservableCollection<ExplorerItem> filteredItems,
        in FileListFilterOptions options)
    {
        for (var i = 0; i < items.Count; i++)
        {
            AddItem(items[i], currentItems, filteredItems, options);
        }
    }

    public static async Task<bool> PopulateCurrentAndFilteredAsync(
        IReadOnlyList<ExplorerItem> source,
        ObservableCollection<ExplorerItem> currentItems,
        ObservableCollection<ExplorerItem> filteredItems,
        FileListFilterOptions options,
        int batchSize,
        Func<bool> canContinue,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < source.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested || !canContinue())
            {
                return false;
            }

            AddItem(source[i], currentItems, filteredItems, options);

            if ((i + 1) % batchSize == 0)
            {
                await Task.Yield();
            }
        }

        return true;
    }

    public static void RebuildRequiredExtensions(HashSet<string> target, IEnumerable<string> requiredExtensions)
    {
        target.Clear();

        foreach (var extension in requiredExtensions)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                continue;
            }

            target.Add(extension.TrimStart('.').ToLowerInvariant());
        }
    }

    public static bool Matches(ExplorerItem item, in FileListFilterOptions options)
    {
        if (item is Folder folder)
        {
            if (!options.ShowFolders)
            {
                return false;
            }

            if (options.IncludeSubdirectories && !string.IsNullOrWhiteSpace(options.CurrentFolderPath))
            {
                var depth = GetDepth(options.CurrentFolderPath, folder.Path);
                if (depth > 1)
                {
                    return false;
                }
            }
        }

        if (!options.ShowHiddenFiles && !options.CurrentItemsExcludeHidden && IsHiddenItem(item))
            return false;

        if (!MatchesExtensionFilter(item, options.RequiredExtensions))
        {
            return false;
        }

        if (options.RequiredTagIds.Count == 0 && options.DisallowedTagIds.Count == 0)
        {
            return true;
        }

        return options.MatchesTagFilter?.Invoke(item) == true;
    }

    private static int GetDepth(string rootPath, string currentPath)
    {
        if (currentPath.Length <= rootPath.Length)
            return 0;

        if (!currentPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            return 0;

        var relative = currentPath[rootPath.Length..]
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.IsNullOrWhiteSpace(relative))
            return 0;

        return relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries).Length;
    }

    public static async Task RebuildFilteredItemsAsync(
        IReadOnlyList<ExplorerItem> source,
        ObservableCollection<ExplorerItem> destination,
        FileListFilterOptions options,
        int batchSize,
        CancellationToken cancellationToken)
    {
        destination.Clear();

        for (var i = 0; i < source.Count; i++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var item = source[i];
            if (Matches(item, options))
            {
                destination.Add(item);
            }

            if ((i + 1) % batchSize == 0)
            {
                await Task.Yield();
            }
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

    private static bool MatchesExtensionFilter(ExplorerItem item, IReadOnlySet<string> requiredExtensions)
    {
        if (requiredExtensions.Count == 0)
            return true;

        if (item is not File fileItem)
            return false;

        return requiredExtensions.Contains(fileItem.Extension);
    }
}
