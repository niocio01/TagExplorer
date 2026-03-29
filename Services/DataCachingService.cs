using CommunityToolkit.Mvvm.Messaging;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO;
using TagExplorer.Data;
using TagExplorer.Models;
using TagExplorer.Models.Messages;
using File = TagExplorer.Models.File;

namespace TagExplorer.Services;

public class DataCachingService
{
    private AppDbContext _db;
    private ILogger<ItemSearchService>? _logger;

    private readonly List<ExplorerItem> _itemCache = [];
    private readonly Dictionary<string, HashSet<ExplorerItem>> _itemsByRootPath = [];
    private readonly Dictionary<string, HashSet<ExplorerItem>> _itemsByExtension = [];

    private readonly List<Tag> _tagCache = [];
    private readonly List<TagAssignment> _tagAssignmentCache = [];
    private readonly List<TagApplication> _tagApplicationCache = [];
    private readonly Dictionary<string, HashSet<TagApplication>> _tagApplicationsByPath = [];
    private readonly Dictionary<int, HashSet<TagApplication>> _tagApplicationsByTagId = [];

    public IReadOnlyList<ExplorerItem> ItemCache => _itemCache;
    public List<Tag> Tags => _tagCache;
    public List<TagAssignment> TagAssignments => _tagAssignmentCache;
    public List<TagApplication> TagApplications => _tagApplicationCache;

    public DataCachingService(AppDbContext appDbContext, ILogger<ItemSearchService>? logger = null)
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
            .Where(a => a.IsArchived == false)
            .ToList());
    }

    public void StartBuild()
    {
        _itemCache.Clear();
    }

    /// <summary>
    /// Adds the EplorerItem to the Cache and updates the indexes for root path and extension.
    /// </summary>
    /// <remarks>
    /// Note: The index for the tags must be built separately by calling BuildTagIndex after all items have been added.
    /// </remarks>

    /// <param name="item">The explorer item to add.</param>
    public void AddItem(
        ExplorerItem item)
    {
        _itemCache.Add(item);

        if (item is Folder folder)
        {
            var rootPath = System.IO.Directory.GetParent(folder.Path)?.FullName;
            if (!string.IsNullOrWhiteSpace(rootPath))
            {
                if (!_itemsByRootPath.TryGetValue(rootPath, out var itemsForRoot))
                {
                    itemsForRoot = new HashSet<ExplorerItem>();
                    _itemsByRootPath[rootPath] = itemsForRoot;
                }
                itemsForRoot.Add(item);
            }
        }


        if (item is File file)
        {
            if (!_itemsByRootPath.TryGetValue(file.JustPath, out var rootPath))
            {
                rootPath = new HashSet<ExplorerItem>();
                _itemsByRootPath[file.JustPath] = rootPath;
            }
            rootPath.Add(file);


            if (!_itemsByExtension.TryGetValue(file.Extension, out var extensionPaths))
            {
                extensionPaths = new HashSet<ExplorerItem>();
                _itemsByExtension[file.Extension] = extensionPaths;
            }

            extensionPaths.Add(file);
        }
    }

    public void BuildTagIndex()
    {
        if (_tagAssignmentCache.Count < 0) return;

        foreach (var assignment in _tagAssignmentCache)
        {
            var item = FindCachedItemByTargetPath(assignment.TargetPath);
            if (item is null)
            {
                _logger.LogWarning("No cached EplorerItem found for TagAssignment with id {TagAssignmentId} and target path {TargetPath}", assignment.Id, assignment.TargetPath);
                continue;
            }

            var tag = _tagCache.FirstOrDefault(t => t.Id == assignment.TagId.Value);
            if (tag == null)
            {
                _logger.LogWarning("Tag with id {TagId} not found for TagAssignment with id {TagAssignmentId}", assignment.TagId.Value, assignment.Id);
                continue;
            }

            switch (assignment.Kind)
            {
                case AssignmentKind.Manual:
                    {
                        // create new application and save it
                        var application = new TagApplication(item, tag, assignment);
                        CacheAndIndexApplication(application);
                    }
                    break;

                case AssignmentKind.AutoDirectChildrenAsChildTag:
                    // TODO: Implement this assignment kind by finding all direct children of the target item and adding them to the index with the same tag id.
                    break;
                default:
                    break;
            }
        }

        WeakReferenceMessenger.Default.Send(new TagApplicationsChangedMessage());
    }

    private void CacheAndIndexApplication(TagApplication application)
    {
        // return if it allready exists.
        if (_tagApplicationCache.Any(app => app.Equals(application)))
            return;

        _tagApplicationCache.Add(application);

        // cache it
        if (!_tagApplicationsByTagId.TryGetValue(application.Tag.Id.Value, out var applicationsOfTag))
        {
            applicationsOfTag = [];
            _tagApplicationsByTagId[application.Tag.Id.Value] = applicationsOfTag;
        }
        applicationsOfTag.Add(application);

        if (!_tagApplicationsByPath.TryGetValue(application.Assignment.TargetPath, out var applicationsForPath))
        {
            applicationsForPath = [];
            _tagApplicationsByPath[application.Assignment.TargetPath] = applicationsForPath;
        }
        applicationsForPath.Add(application);
    }

    private ExplorerItem? FindCachedItemByTargetPath(string targetPath)
    {
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            return null;
        }
        var parentPath = System.IO.Directory.GetParent(targetPath).FullName;

        if (!_itemsByRootPath.TryGetValue(parentPath, out var itemsAtPath) || itemsAtPath.Count == 0)
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
        if (!_tagApplicationsByPath.TryGetValue(path, out var tagApplicationsForPath))
        {
            return [];
        }
        return tagApplicationsForPath.ToList();
    }

    public IReadOnlyList<TagApplication> GetTagApplicationsForItem(ExplorerItem item) => GetTagApplicationsForPath(item.Path);

    public IReadOnlyList<TagApplication> GetTagApplicationsForTag(int TagId)
    {
        if (!_tagApplicationsByTagId.TryGetValue(TagId, out var applicationsOfTag))
        {
            return [];
        }
        return applicationsOfTag.ToList();
    }

    public IReadOnlyList<ExplorerItem> GetCandidates(
        string? rootPath,
        IReadOnlySet<int> requiredTagIds,
        IReadOnlySet<int> disallowedTagIds,
        IReadOnlySet<string> requiredExtensions)
    {
        var candidates = new HashSet<ExplorerItem>(_itemCache);

        FilterByRootPath(candidates, rootPath);

        if (requiredTagIds.Count > 0)
        {
            foreach (var tagId in requiredTagIds)
            {
                if (!_tagApplicationsByTagId.TryGetValue(tagId, out var applicationsOfTag))
                {
                    return [];
                }

                candidates.IntersectWith(applicationsOfTag.Select(a => a.ExplorerItem));
            }
        }

        if (disallowedTagIds.Count > 0)
        {
            foreach (var tagId in disallowedTagIds)
            {
                if (_tagApplicationsByTagId.TryGetValue(tagId, out var applicationsOfTag))
                {
                    candidates.ExceptWith(applicationsOfTag.Select(a => a.ExplorerItem));
                }
            }
        }

        if (requiredExtensions.Count > 0)
        {
            foreach (var extension in requiredExtensions)
            {
                if (_itemsByExtension.TryGetValue(extension, out var itemsWithRequiredExtention))
                {
                    candidates.IntersectWith(itemsWithRequiredExtention);
                }
            }
        }

        return candidates.ToList();
    }

    public bool AddTagApplication(TagApplication application)
    {
        if (application is null)
            return false;

        // add Assignment to cache and db if not allready known
        if (!_tagAssignmentCache.Any(ass => ass.Equals(application.Assignment)))
        {
            _tagAssignmentCache.Add(application.Assignment);
            _db.TagAssignments.Add(application.Assignment);
            _db.SaveChanges();
        }

        CacheAndIndexApplication(application);

        WeakReferenceMessenger.Default.Send(new TagApplicationsChangedMessage(application.ExplorerItem.Path));

        return true;
    }

    public bool RemoveTagApplication(TagApplication application)
    {
        if (application is null)
            return false;


        var assignment = application.Assignment;
        RemoveApplicationFromCacheAndIndex(application);

        // Remove assignment, if not used anymore
        if (_tagAssignmentCache.Count(ass => ass.Id == application.Assignment.Id) <= 1)
        {
            _tagAssignmentCache.Remove(application.Assignment);
            var dbAssigment = _db.TagAssignments.FirstOrDefault(ass => ass.Id == application.Assignment.Id);
            if (dbAssigment == null) return true;
            dbAssigment.IsArchived = true;
            dbAssigment.Enabled = false;
            dbAssigment.UpdatedAtUtc = DateTime.UtcNow;
            _db.SaveChanges();
        }

        WeakReferenceMessenger.Default.Send(new TagApplicationsChangedMessage(application.ExplorerItem.Path));

        return true;
    }

    private void RemoveApplicationFromCacheAndIndex(TagApplication application)
    {
        // remove from Indexes
        if (_tagApplicationsByTagId.TryGetValue(application.Tag.Id.Value, out var applicationsOfTagById))
        {
            if (applicationsOfTagById.Count <= 1)
            {
                _tagApplicationsByTagId.Remove(application.Tag.Id.Value);
            }
            else
            {
                applicationsOfTagById.Remove(application);
            }
        }

        if (_tagApplicationsByPath.TryGetValue(application.ExplorerItem.Path, out var applicationsOfTagByPath))
        {
            if (applicationsOfTagByPath.Count <= 1)
            {
                _tagApplicationsByPath.Remove(application.ExplorerItem.Path);
            }
            else
            {
                applicationsOfTagByPath.Remove(application);
            }
        }
        _tagApplicationCache.Remove(application);
    }

    private void FilterByRootPath(
    HashSet<ExplorerItem> items,
    string? rootPath)
    {
        if (items.Count == 0 || string.IsNullOrWhiteSpace(rootPath))
        {
            return;
        }

        if (!_itemsByRootPath.TryGetValue(rootPath, out var rootItems) || rootItems.Count == 0)
        {
            items.Clear();
            return;
        }

        var allowedPaths = _itemsByRootPath.Keys
            .Where(
                path => string.Equals(path, rootPath, StringComparison.OrdinalIgnoreCase)
                || path.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            .ToList();

        HashSet<ExplorerItem> allowedItems = new HashSet<ExplorerItem>();
        foreach (var path in allowedPaths)
        {
            if (_itemsByRootPath.TryGetValue(path, out var itemsOfPath))
            {
                allowedItems.UnionWith(itemsOfPath);
            }
        }

        items.IntersectWith(allowedItems);
    }



    /// <summary>
    /// Clear Cache and all indexes.
    /// </summary>
    public void Clear()
    {
        _itemCache.Clear();
        _itemsByRootPath.Clear();
        _itemsByExtension.Clear();

        _tagCache.Clear();
        _tagAssignmentCache.Clear();
        _tagApplicationCache.Clear();
        _tagApplicationsByPath.Clear();
        _tagApplicationsByTagId.Clear();
    }

}
