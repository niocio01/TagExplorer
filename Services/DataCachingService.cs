using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO;
using TagExplorer.Data;
using TagExplorer.Models;
using TagExplorer.Models.Messages;
using File = TagExplorer.Models.File;

namespace TagExplorer.Services;

/// <summary>
/// Holds the item cache with its indexes and the tag applications of the cached items.
/// </summary>
/// <remarks>
/// A cache build fills a separate pending index (StartBuild, AddItem) and publishes it as a whole
/// (CompleteBuild), so queries never see a half-built cache. All other shared state is guarded by
/// <see cref="_sync"/>, because queries run on worker threads while tags are changed on the UI thread.
/// </remarks>
public class DataCachingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<DataCachingService>? _logger;
    private readonly object _sync = new();

    private ItemIndex _items = ItemIndex.Empty;
    private ItemIndex? _pendingItems;

    private readonly List<Tag> _tagCache = [];
    private readonly List<TagAssignment> _tagAssignmentCache = [];
    private readonly List<TagApplication> _tagApplicationCache = [];
    private readonly Dictionary<string, HashSet<TagApplication>> _tagApplicationsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, HashSet<TagApplication>> _tagApplicationsByTagId = [];

    /// <summary>
    /// The published item cache. The list is never changed after publishing.
    /// </summary>
    public IReadOnlyList<ExplorerItem> ItemCache => _items.Items;

    public IReadOnlyList<Tag> Tags
    {
        get { lock (_sync) return _tagCache.ToList(); }
    }

    public IReadOnlyList<TagAssignment> TagAssignments
    {
        get { lock (_sync) return _tagAssignmentCache.ToList(); }
    }

    public IReadOnlyList<TagApplication> TagApplications
    {
        get { lock (_sync) return _tagApplicationCache.ToList(); }
    }

    public DataCachingService(AppDbContext appDbContext, ILogger<DataCachingService>? logger = null)
    {
        _db = appDbContext;
        _logger = logger;

        var cachedTagDtos = _db.Tags
            .AsNoTracking()
            .Include(dto => dto.Color)
            .Include(dto => dto.Parent)
                .ThenInclude(parent => parent.Color)
            .ToList();

        _tagCache.AddRange(cachedTagDtos.Select(dto => new Tag(dto)));
        _tagAssignmentCache.AddRange(
            _db.TagAssignments.AsNoTracking()
            .Where(a => !a.IsArchived && a.Enabled && a.TagId != null)
            .ToList());
    }

    /// <summary>
    /// Starts a new cache build. Items added with <see cref="AddItem"/> stay invisible until
    /// <see cref="CompleteBuild"/> publishes them.
    /// </summary>
    public void StartBuild()
    {
        _pendingItems = new ItemIndex();
    }

    /// <summary>
    /// Adds the item to the pending cache build. Must be called from one thread at a time.
    /// </summary>
    /// <param name="item">The explorer item to add.</param>
    public void AddItem(ExplorerItem item)
    {
        var pendingItems = _pendingItems
            ?? throw new InvalidOperationException("StartBuild must be called before AddItem.");
        pendingItems.Add(item);
    }

    /// <summary>
    /// Discards the pending cache build and keeps the published cache.
    /// </summary>
    public void AbortBuild()
    {
        _pendingItems = null;
    }

    /// <summary>
    /// Publishes the pending cache build and rebuilds the tag applications for the new items.
    /// </summary>
    public void CompleteBuild()
    {
        var pendingItems = _pendingItems
            ?? throw new InvalidOperationException("StartBuild must be called before CompleteBuild.");
        _pendingItems = null;

        lock (_sync)
        {
            _items = pendingItems;
            ClearTagApplications();

            foreach (var assignment in _tagAssignmentCache)
            {
                ApplyAssignment(assignment);
            }
        }

        WeakReferenceMessenger.Default.Send(new TagApplicationsChangedMessage());
    }

    private void ApplyAssignment(TagAssignment assignment)
    {
        var item = FindCachedItemByTargetPath(assignment.TargetPath);
        if (item is null)
        {
            _logger?.LogWarning("No cached ExplorerItem found for TagAssignment with id {TagAssignmentId} and target path {TargetPath}", assignment.Id, assignment.TargetPath);
            return;
        }

        var tag = _tagCache.FirstOrDefault(t => t.Id == assignment.TagId);
        if (tag is null)
        {
            _logger?.LogWarning("Tag with id {TagId} not found for TagAssignment with id {TagAssignmentId}", assignment.TagId, assignment.Id);
            return;
        }

        switch (assignment.Kind)
        {
            case AssignmentKind.Manual:
                CacheAndIndexApplication(new TagApplication(item, tag, assignment));
                break;

            case AssignmentKind.AutoDirectChildrenAsChildTag:
                // TODO: Implement this assignment kind by finding all direct children of the target item and adding them to the index with the same tag id.
                break;

            default:
                break;
        }
    }

    private void CacheAndIndexApplication(TagApplication application)
    {
        var path = application.ExplorerItem.Path;

        if (_tagApplicationsByPath.TryGetValue(path, out var applicationsForPath)
            && applicationsForPath.Any(app => app.Assignment == application.Assignment && app.ExplorerItem == application.ExplorerItem))
        {
            return;
        }

        _tagApplicationCache.Add(application);

        var tagId = application.Tag.Id!.Value;
        if (!_tagApplicationsByTagId.TryGetValue(tagId, out var applicationsOfTag))
        {
            applicationsOfTag = [];
            _tagApplicationsByTagId[tagId] = applicationsOfTag;
        }
        applicationsOfTag.Add(application);

        if (applicationsForPath is null)
        {
            applicationsForPath = [];
            _tagApplicationsByPath[path] = applicationsForPath;
        }
        applicationsForPath.Add(application);
    }

    private ExplorerItem? FindCachedItemByTargetPath(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return null;
        }

        var parentPath = Directory.GetParent(targetPath)?.FullName;
        if (parentPath is null
            || !_items.ItemsByParentPath.TryGetValue(parentPath, out var itemsAtPath))
        {
            return null;
        }

        foreach (var item in itemsAtPath)
        {
            switch (item)
            {
                case File file when string.Equals(file.FullPath, targetPath, StringComparison.OrdinalIgnoreCase):
                    return file;

                case Folder folder when string.Equals(folder.Path, targetPath, StringComparison.OrdinalIgnoreCase):
                    return folder;
            }
        }

        return null;
    }

    public IReadOnlyList<TagApplication> GetTagApplicationsForPath(string path)
    {
        lock (_sync)
        {
            return _tagApplicationsByPath.TryGetValue(path, out var applicationsForPath)
                ? applicationsForPath.ToList()
                : [];
        }
    }

    public IReadOnlyList<TagApplication> GetTagApplicationsForItem(ExplorerItem item) => GetTagApplicationsForPath(item.Path);

    public IReadOnlyList<TagApplication> GetTagApplicationsForTag(int tagId)
    {
        lock (_sync)
        {
            return _tagApplicationsByTagId.TryGetValue(tagId, out var applicationsOfTag)
                ? applicationsOfTag.ToList()
                : [];
        }
    }

    public IReadOnlyList<ExplorerItem> GetCandidates(
        string? rootPath,
        IReadOnlySet<int> requiredTagIds,
        IReadOnlySet<int> disallowedTagIds,
        IReadOnlySet<string> requiredExtensions,
        IReadOnlySet<string> disallowedExtensions)
    {
        // Take consistent snapshots under the lock; the set operations below run without it.
        ItemIndex items;
        var itemsPerRequiredTag = new List<HashSet<ExplorerItem>>(requiredTagIds.Count);
        var itemsWithDisallowedTag = new HashSet<ExplorerItem>();

        lock (_sync)
        {
            items = _items;

            foreach (var tagId in requiredTagIds)
            {
                if (!_tagApplicationsByTagId.TryGetValue(tagId, out var applicationsOfTag))
                {
                    return [];
                }

                itemsPerRequiredTag.Add(applicationsOfTag.Select(a => a.ExplorerItem).ToHashSet());
            }

            foreach (var tagId in disallowedTagIds)
            {
                if (_tagApplicationsByTagId.TryGetValue(tagId, out var applicationsOfTag))
                {
                    itemsWithDisallowedTag.UnionWith(applicationsOfTag.Select(a => a.ExplorerItem));
                }
            }
        }

        var candidates = string.IsNullOrWhiteSpace(rootPath)
            ? new HashSet<ExplorerItem>(items.Items)
            : GetItemsBelow(items, rootPath);

        foreach (var itemsWithTag in itemsPerRequiredTag)
        {
            candidates.IntersectWith(itemsWithTag);
        }

        candidates.ExceptWith(itemsWithDisallowedTag);

        if (requiredExtensions.Count > 0)
        {
            // An item matches if it has any of the required extensions.
            var itemsWithAnyRequiredExtension = new HashSet<ExplorerItem>();
            foreach (var extension in requiredExtensions)
            {
                var normalizedExtension = extension.TrimStart('.').ToLowerInvariant();
                if (items.ItemsByExtension.TryGetValue(normalizedExtension, out var itemsWithExtension))
                {
                    itemsWithAnyRequiredExtension.UnionWith(itemsWithExtension);
                }
            }

            candidates.IntersectWith(itemsWithAnyRequiredExtension);
        }

        foreach (var extension in disallowedExtensions)
        {
            var normalizedExtension = extension.TrimStart('.').ToLowerInvariant();
            if (items.ItemsByExtension.TryGetValue(normalizedExtension, out var itemsWithExtension))
            {
                candidates.ExceptWith(itemsWithExtension);
            }
        }

        return candidates.ToList();
    }

    public bool AddTagApplication(TagApplication application)
    {
        if (application is null)
            return false;

        bool isNewAssignment;
        lock (_sync)
        {
            isNewAssignment = !_tagAssignmentCache.Contains(application.Assignment);
        }

        // Store a new assignment before caching it, so the cache never holds unsaved assignments.
        if (isNewAssignment)
        {
            _db.TagAssignments.Add(application.Assignment);
            _db.SaveChanges();
        }

        lock (_sync)
        {
            if (isNewAssignment)
            {
                _tagAssignmentCache.Add(application.Assignment);
            }

            // Tags created after startup are not in the cache yet; a later rebuild needs them.
            if (application.Tag.Id is not null && _tagCache.All(t => t.Id != application.Tag.Id))
            {
                _tagCache.Add(application.Tag);
            }

            CacheAndIndexApplication(application);
        }

        WeakReferenceMessenger.Default.Send(new TagApplicationsChangedMessage(application.ExplorerItem.Path));

        return true;
    }

    public bool RemoveTagApplication(TagApplication application)
    {
        if (application is null)
            return false;

        var assignment = application.Assignment;
        bool isAssignmentUnused;

        lock (_sync)
        {
            RemoveApplicationFromCacheAndIndex(application);

            isAssignmentUnused = !_tagApplicationCache.Any(app => app.Assignment == assignment);
            if (isAssignmentUnused)
            {
                _tagAssignmentCache.Remove(assignment);
            }
        }

        // Archive the assignment once no application uses it anymore.
        if (isAssignmentUnused)
        {
            var dbAssignment = _db.TagAssignments.FirstOrDefault(ass => ass.Id == assignment.Id);
            if (dbAssignment is not null)
            {
                dbAssignment.IsArchived = true;
                dbAssignment.Enabled = false;
                dbAssignment.UpdatedAtUtc = DateTime.UtcNow;
                _db.SaveChanges();
            }
        }

        WeakReferenceMessenger.Default.Send(new TagApplicationsChangedMessage(application.ExplorerItem.Path));

        return true;
    }

    private void RemoveApplicationFromCacheAndIndex(TagApplication application)
    {
        var tagId = application.Tag.Id!.Value;
        if (_tagApplicationsByTagId.TryGetValue(tagId, out var applicationsOfTag))
        {
            applicationsOfTag.Remove(application);
            if (applicationsOfTag.Count == 0)
            {
                _tagApplicationsByTagId.Remove(tagId);
            }
        }

        var path = application.ExplorerItem.Path;
        if (_tagApplicationsByPath.TryGetValue(path, out var applicationsForPath))
        {
            applicationsForPath.Remove(application);
            if (applicationsForPath.Count == 0)
            {
                _tagApplicationsByPath.Remove(path);
            }
        }

        _tagApplicationCache.Remove(application);
    }

    private static HashSet<ExplorerItem> GetItemsBelow(ItemIndex items, string rootPath)
    {
        var result = new HashSet<ExplorerItem>();
        var pathPrefix = rootPath + Path.DirectorySeparatorChar;

        foreach (var (parentPath, itemsOfPath) in items.ItemsByParentPath)
        {
            if (string.Equals(parentPath, rootPath, StringComparison.OrdinalIgnoreCase)
                || parentPath.StartsWith(pathPrefix, StringComparison.OrdinalIgnoreCase))
            {
                result.UnionWith(itemsOfPath);
            }
        }

        return result;
    }

    private void ClearTagApplications()
    {
        _tagApplicationCache.Clear();
        _tagApplicationsByPath.Clear();
        _tagApplicationsByTagId.Clear();
    }

    /// <summary>
    /// Clears the item cache and the tag applications. Tags and tag assignments stay cached,
    /// because they are only loaded at startup.
    /// </summary>
    public void Clear()
    {
        _pendingItems = null;

        lock (_sync)
        {
            _items = ItemIndex.Empty;
            ClearTagApplications();
        }

        WeakReferenceMessenger.Default.Send(new TagApplicationsChangedMessage());
    }

    /// <summary>
    /// The cached items with lookups by parent path and by extension.
    /// </summary>
    private sealed class ItemIndex
    {
        public static readonly ItemIndex Empty = new();

        public List<ExplorerItem> Items { get; } = [];
        public Dictionary<string, HashSet<ExplorerItem>> ItemsByParentPath { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, HashSet<ExplorerItem>> ItemsByExtension { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void Add(ExplorerItem item)
        {
            Items.Add(item);

            var parentPath = item switch
            {
                File file => file.JustPath,
                Folder folder => Directory.GetParent(folder.Path)?.FullName,
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(parentPath))
            {
                AddToIndex(ItemsByParentPath, parentPath, item);
            }

            if (item is File fileItem)
            {
                AddToIndex(ItemsByExtension, fileItem.Extension, item);
            }
        }

        private static void AddToIndex(Dictionary<string, HashSet<ExplorerItem>> index, string key, ExplorerItem item)
        {
            if (!index.TryGetValue(key, out var items))
            {
                items = [];
                index[key] = items;
            }

            items.Add(item);
        }
    }
}
