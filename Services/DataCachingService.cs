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

    // Tags reaching items through assignments on folders above them (virtual tags). Built on demand from the
    // assignments and items, and cleared whenever either changes (InvalidateInheritedCaches).
    private Dictionary<string, List<TagAssignment>>? _assignmentsReachingOtherItemsByPath;
    private readonly Dictionary<string, List<TagApplication>> _inheritedApplicationsByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, HashSet<ExplorerItem>> _inheritedItemsByTagId = [];

    // Child tags of child-tag rules, one per direct subfolder ("parentTagId|subfolder path" keys, see ChildTagKey).
    // Generated tags only live here; stored overrides are in _tagCache, hidden ones in _hiddenChildTags.
    private Dictionary<string, Tag> _generatedChildTagsByKey = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Tag> _childTagsByKey = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<Tag> _hiddenChildTags = [];
    private int _nextGeneratedTagId = -1;

    private const int MaxTagNameLength = 50;

    /// <summary>
    /// The published item cache. The list is never changed after publishing.
    /// </summary>
    public IReadOnlyList<ExplorerItem> ItemCache => _items.Items;

    /// <summary>
    /// The stored tags and the generated child tags of child-tag rules.
    /// </summary>
    public IReadOnlyList<Tag> Tags
    {
        get { lock (_sync) return _tagCache.Concat(_generatedChildTagsByKey.Values).ToList(); }
    }

    /// <summary>
    /// Stored child tags that hide the generated tag of their subfolder.
    /// </summary>
    public IReadOnlyList<Tag> HiddenChildTags
    {
        get { lock (_sync) return _hiddenChildTags.ToList(); }
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

        // Archived child tags only mark their subfolder as hidden; they are no usable tags.
        _tagCache.AddRange(cachedTagDtos.Where(dto => !IsHiddenChildTag(dto)).Select(dto => new Tag(dto)));
        _hiddenChildTags.AddRange(cachedTagDtos.Where(IsHiddenChildTag).Select(dto => new Tag(dto)));
        _tagAssignmentCache.AddRange(
            _db.TagAssignments.AsNoTracking()
            .Where(a => !a.IsArchived && a.Enabled && (a.TagId != null || a.AutoRuleParentTagId != null))
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

            RebuildChildTags();
        }

        WeakReferenceMessenger.Default.Send(new TagsChangedMessage());
        WeakReferenceMessenger.Default.Send(new TagApplicationsChangedMessage());
    }

    private static bool IsHiddenChildTag(TagDTO dto)
    {
        return dto.AutoSourcePath is not null && dto.IsArchived;
    }

    private static string ChildTagKey(int parentTagId, string subfolderPath)
    {
        return $"{parentTagId}|{subfolderPath}";
    }

    /// <summary>
    /// Works out the child tag of every subfolder of every child-tag rule: a stored override if there is one,
    /// nothing if hidden, otherwise a generated tag. Generated tags keep their object and id across rebuilds, so
    /// filter chips holding them stay valid. Must be called with <see cref="_sync"/> held.
    /// </summary>
    private void RebuildChildTags()
    {
        var overridesByKey = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);
        foreach (var tag in _tagCache.Where(t => t.AutoSourcePath is not null && t.Parent?.Id is not null))
        {
            overridesByKey.TryAdd(ChildTagKey(tag.Parent!.Id!.Value, tag.AutoSourcePath!), tag);
        }

        var hiddenKeys = _hiddenChildTags
            .Where(t => t.AutoSourcePath is not null && t.Parent?.Id is not null)
            .Select(t => ChildTagKey(t.Parent!.Id!.Value, t.AutoSourcePath!))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var generatedByKey = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);
        var childTagsByKey = new Dictionary<string, Tag>(StringComparer.OrdinalIgnoreCase);

        foreach (var rule in _tagAssignmentCache.Where(a => a.Kind == AssignmentKind.AutoDirectChildrenAsChildTag))
        {
            if (FindCachedTag(rule.AutoRuleParentTagId) is not Tag parentTag || parentTag.Id is not int parentTagId)
            {
                continue;
            }

            foreach (var subfolder in GetDirectSubfolders(_items, rule.TargetPath))
            {
                var key = ChildTagKey(parentTagId, subfolder.Path);
                if (childTagsByKey.ContainsKey(key) || hiddenKeys.Contains(key))
                {
                    continue;
                }

                if (overridesByKey.TryGetValue(key, out var storedTag))
                {
                    childTagsByKey[key] = storedTag;
                    continue;
                }

                var generatedTag = _generatedChildTagsByKey.TryGetValue(key, out var existingTag)
                    ? existingTag
                    : Tag.CreateGenerated(_nextGeneratedTagId--, ToChildTagName(subfolder.Name), parentTag, subfolder.Path);

                generatedByKey[key] = generatedTag;
                childTagsByKey[key] = generatedTag;
            }
        }

        _generatedChildTagsByKey = generatedByKey;
        _childTagsByKey = childTagsByKey;
        InvalidateInheritedCaches();
    }

    /// <summary>
    /// True if the stored child tag currently stands for a subfolder of an active rule.
    /// </summary>
    public bool IsActiveChildTag(int tagId)
    {
        lock (_sync)
        {
            return _childTagsByKey.Values.Any(t => t.Id == tagId);
        }
    }

    // Adds the application of an assignment to its own target. Rules that only tag items below their
    // target (direct subfolders, child tags) have none; they are resolved by GetInheritedApplications.
    private void ApplyAssignment(TagAssignment assignment, ExplorerItem? targetItem = null)
    {
        if (!AssignmentModes.AppliesToTarget(assignment))
        {
            return;
        }

        // Items outside the cache (e.g. base folders, or before the first build) are passed in by the caller.
        var item = targetItem ?? FindCachedItemByTargetPath(assignment.TargetPath);
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

        CacheAndIndexApplication(new TagApplication(item, tag, assignment));
    }

    private Tag? FindCachedTag(int? tagId)
    {
        if (tagId is null)
        {
            return null;
        }

        return tagId < 0
            ? _generatedChildTagsByKey.Values.FirstOrDefault(t => t.Id == tagId)
            : _tagCache.FirstOrDefault(t => t.Id == tagId);
    }

    private Tag? FindChildTag(int parentTagId, string subfolderPath)
    {
        return _childTagsByKey.GetValueOrDefault(ChildTagKey(parentTagId, subfolderPath));
    }

    private static string ToChildTagName(string folderName)
    {
        return folderName.Length <= MaxTagNameLength ? folderName : folderName[..MaxTagNameLength];
    }

    private static IEnumerable<Folder> GetDirectSubfolders(ItemIndex items, string folderPath)
    {
        return items.ItemsByParentPath.TryGetValue(folderPath, out var children)
            ? children.OfType<Folder>()
            : [];
    }

    private Dictionary<string, List<TagAssignment>> GetAssignmentsReachingOtherItemsByPath()
    {
        if (_assignmentsReachingOtherItemsByPath is not null)
        {
            return _assignmentsReachingOtherItemsByPath;
        }

        _assignmentsReachingOtherItemsByPath = _tagAssignmentCache
            .Where(AssignmentModes.ReachesOtherItems)
            .GroupBy(a => a.TargetPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        return _assignmentsReachingOtherItemsByPath;
    }

    private void InvalidateInheritedCaches()
    {
        _assignmentsReachingOtherItemsByPath = null;
        _inheritedApplicationsByPath.Clear();
        _inheritedItemsByTagId.Clear();
    }

    /// <summary>
    /// The virtual tag applications of the item at the path, inherited from assignments on the folders above it.
    /// Must be called with <see cref="_sync"/> held.
    /// </summary>
    private List<TagApplication> GetInheritedApplications(string path)
    {
        if (_inheritedApplicationsByPath.TryGetValue(path, out var cachedApplications))
        {
            return cachedApplications;
        }

        var applications = new List<TagApplication>();
        var assignmentsByPath = GetAssignmentsReachingOtherItemsByPath();
        var item = assignmentsByPath.Count > 0 ? FindCachedItemByTargetPath(path) : null;

        if (item is not null)
        {
            // Walk up the folders; childPath is the folder (or the item) directly below the current ancestor.
            var childPath = path;
            var ancestorPath = Path.GetDirectoryName(path);
            var depth = 1;

            while (!string.IsNullOrEmpty(ancestorPath))
            {
                if (assignmentsByPath.TryGetValue(ancestorPath, out var assignments))
                {
                    foreach (var assignment in assignments)
                    {
                        AddInheritedApplications(applications, item, assignment, depth, childPath);
                    }
                }

                childPath = ancestorPath;
                ancestorPath = Path.GetDirectoryName(ancestorPath);
                depth++;
            }
        }

        _inheritedApplicationsByPath[path] = applications;
        return applications;
    }

    private void AddInheritedApplications(List<TagApplication> applications, ExplorerItem item, TagAssignment assignment, int depth, string childPath)
    {
        void Add(Tag? tag)
        {
            if (tag?.Id is not null)
            {
                applications.Add(new TagApplication(item, tag, assignment));
            }
        }

        switch (assignment.Kind, assignment.Scope)
        {
            case (AssignmentKind.AutoDirectChildrenAsChildTag, _) when assignment.AutoRuleParentTagId is int parentTagId:
                // childPath is a direct subfolder of the rule's folder, unless depth 1 names a file.
                if (depth == 1 && item is not Folder)
                {
                    break;
                }

                if (depth == 1)
                {
                    Add(FindCachedTag(parentTagId));
                }

                Add(FindChildTag(parentTagId, childPath));
                break;

            case (AssignmentKind.Manual, ApplyScope.SelfAndAllDescendants):
                Add(FindCachedTag(assignment.TagId));
                break;

            case (AssignmentKind.Manual, ApplyScope.SelfAndDirectDescendants) when depth == 1:
                Add(FindCachedTag(assignment.TagId));
                break;

            case (AssignmentKind.Manual, ApplyScope.DirectChildren) when depth == 1 && item is Folder:
                Add(FindCachedTag(assignment.TagId));
                break;
        }
    }

    /// <summary>
    /// The items carrying the tag through assignments on folders above them. Must be called with <see cref="_sync"/> held.
    /// </summary>
    private HashSet<ExplorerItem> GetInheritedItemsWithTag(int tagId)
    {
        if (_inheritedItemsByTagId.TryGetValue(tagId, out var cachedItems))
        {
            return cachedItems;
        }

        var result = new HashSet<ExplorerItem>();
        var tag = FindCachedTag(tagId);

        foreach (var (targetPath, assignments) in GetAssignmentsReachingOtherItemsByPath())
        {
            foreach (var assignment in assignments)
            {
                switch (assignment.Kind, assignment.Scope)
                {
                    case (AssignmentKind.AutoDirectChildrenAsChildTag, _):
                        if (assignment.AutoRuleParentTagId == tagId)
                        {
                            result.UnionWith(GetDirectSubfolders(_items, targetPath));
                        }
                        else if (tag?.Parent?.Id is int parentTagId && parentTagId == assignment.AutoRuleParentTagId)
                        {
                            foreach (var subfolder in GetDirectSubfolders(_items, targetPath)
                                .Where(folder => FindChildTag(parentTagId, folder.Path) == tag))
                            {
                                result.Add(subfolder);
                                result.UnionWith(GetItemsBelow(_items, subfolder.Path));
                            }
                        }
                        break;

                    case (AssignmentKind.Manual, ApplyScope.SelfAndAllDescendants) when assignment.TagId == tagId:
                        result.UnionWith(GetItemsBelow(_items, targetPath));
                        break;

                    case (AssignmentKind.Manual, ApplyScope.SelfAndDirectDescendants) when assignment.TagId == tagId:
                        if (_items.ItemsByParentPath.TryGetValue(targetPath, out var children))
                        {
                            result.UnionWith(children);
                        }
                        break;

                    case (AssignmentKind.Manual, ApplyScope.DirectChildren) when assignment.TagId == tagId:
                        result.UnionWith(GetDirectSubfolders(_items, targetPath));
                        break;
                }
            }
        }

        _inheritedItemsByTagId[tagId] = result;
        return result;
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
        InvalidateInheritedCaches();

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

    /// <summary>
    /// The direct and the virtual (inherited) tag applications of the item at the path.
    /// </summary>
    public IReadOnlyList<TagApplication> GetTagApplicationsForPath(string path)
    {
        lock (_sync)
        {
            var applications = _tagApplicationsByPath.TryGetValue(path, out var applicationsForPath)
                ? applicationsForPath.ToList()
                : [];

            applications.AddRange(GetInheritedApplications(path));
            return applications;
        }
    }

    /// <summary>
    /// The assignments on the folder that only tag items below it (direct subfolders, child-tag rules), so they
    /// have no application on the folder itself.
    /// </summary>
    public IReadOnlyList<TagAssignment> GetRulesOnFolder(string folderPath)
    {
        lock (_sync)
        {
            return GetAssignmentsReachingOtherItemsByPath().TryGetValue(folderPath, out var assignments)
                ? assignments.Where(a => !AssignmentModes.AppliesToTarget(a)).ToList()
                : [];
        }
    }

    public ExplorerItem? FindCachedItem(string path)
    {
        lock (_sync)
        {
            return FindCachedItemByTargetPath(path);
        }
    }

    public IReadOnlyList<TagApplication> GetTagApplicationsForItem(ExplorerItem item) => GetTagApplicationsForPath(item.Path);

    /// <summary>
    /// The direct applications of the tag; virtual ones are not included.
    /// </summary>
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
                var itemsWithTag = GetItemsWithTag(tagId);
                if (itemsWithTag.Count == 0)
                {
                    return [];
                }

                itemsPerRequiredTag.Add(itemsWithTag);
            }

            foreach (var tagId in disallowedTagIds)
            {
                itemsWithDisallowedTag.UnionWith(GetItemsWithTag(tagId));
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

    // Items with the tag, directly or virtually. Returns a new set; must be called with _sync held.
    private HashSet<ExplorerItem> GetItemsWithTag(int tagId)
    {
        var items = new HashSet<ExplorerItem>(GetInheritedItemsWithTag(tagId));
        if (_tagApplicationsByTagId.TryGetValue(tagId, out var applicationsOfTag))
        {
            items.UnionWith(applicationsOfTag.Select(a => a.ExplorerItem));
        }

        return items;
    }

    public Tag? GetTag(int tagId)
    {
        lock (_sync)
        {
            return FindCachedTag(tagId);
        }
    }

    /// <summary>
    /// Adds a newly saved tag to the cache, or updates the cached one in place so existing
    /// applications, badges and filter chips keep referencing the same object.
    /// </summary>
    public void UpsertTag(TagDTO dto)
    {
        Tag? cachedTag;
        lock (_sync)
        {
            cachedTag = _tagCache.FirstOrDefault(t => t.Id == dto.Id);
            if (cachedTag is null)
            {
                _tagCache.Add(new Tag(dto));
            }

            // A new tag can be the parent of a rule.
            RebuildChildTags();
        }

        // Outside the lock: this raises PropertyChanged, which updates bound UI.
        cachedTag?.UpdateFrom(dto);

        // Generated child tags look like their parent.
        foreach (var generatedTag in Tags.Where(t => t.IsGenerated && t.Parent?.Id == dto.Id))
        {
            generatedTag.Color = dto.Color;
            generatedTag.IconName = dto.IconName;
        }

        WeakReferenceMessenger.Default.Send(new TagsChangedMessage());
        WeakReferenceMessenger.Default.Send(new TagApplicationsChangedMessage());
    }

    /// <summary>
    /// Removes a deleted tag from the cache. Remove its assignments first (RemoveAssignmentsOfTag).
    /// </summary>
    public void RemoveTag(int tagId)
    {
        lock (_sync)
        {
            _tagCache.RemoveAll(t => t.Id == tagId);
            RebuildChildTags();
        }

        WeakReferenceMessenger.Default.Send(new TagsChangedMessage());
    }

    private void ArchiveAssignment(TagAssignment assignment)
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

    /// <summary>
    /// Saves a new assignment and caches it with its direct and virtual applications. For a child-tag rule,
    /// the tags of its subfolders are generated as well.
    /// </summary>
    /// <param name="targetItem">The item the assignment sits on, if known; needed for items outside the cache.</param>
    public void AddAssignment(TagAssignment assignment, ExplorerItem? targetItem = null)
    {
        _db.TagAssignments.Add(assignment);
        _db.SaveChanges();

        lock (_sync)
        {
            _tagAssignmentCache.Add(assignment);
            ApplyAssignment(assignment, targetItem);
            RebuildChildTags();
        }

        if (assignment.Kind == AssignmentKind.AutoDirectChildrenAsChildTag)
        {
            WeakReferenceMessenger.Default.Send(new TagsChangedMessage());
        }

        var affectedPath = AssignmentModes.ReachesOtherItems(assignment) ? null : assignment.TargetPath;
        WeakReferenceMessenger.Default.Send(new TagApplicationsChangedMessage(affectedPath));
    }

    /// <summary>
    /// Removes the assignment with all its direct and virtual applications and archives it.
    /// </summary>
    public void RemoveAssignment(TagAssignment assignment)
    {
        lock (_sync)
        {
            foreach (var application in _tagApplicationCache.Where(app => app.Assignment == assignment).ToList())
            {
                RemoveApplicationFromCacheAndIndex(application);
            }

            _tagAssignmentCache.Remove(assignment);
            RebuildChildTags();
        }

        ArchiveAssignment(assignment);

        if (assignment.Kind == AssignmentKind.AutoDirectChildrenAsChildTag)
        {
            WeakReferenceMessenger.Default.Send(new TagsChangedMessage());
        }

        WeakReferenceMessenger.Default.Send(new TagApplicationsChangedMessage());
    }

    /// <summary>
    /// Removes all assignments using the tag, as assigned tag or as parent tag of a child-tag rule.
    /// </summary>
    public void RemoveAssignmentsOfTag(int tagId)
    {
        List<TagAssignment> assignments;
        lock (_sync)
        {
            assignments = _tagAssignmentCache.Where(a => a.TagId == tagId || a.AutoRuleParentTagId == tagId).ToList();
        }

        foreach (var assignment in assignments)
        {
            RemoveAssignment(assignment);
        }
    }

    /// <summary>
    /// Stores a generated child tag, so it can be customised or assigned. The cached object is updated in place
    /// (real id, no longer generated), so filter chips and badges holding it keep working. Stored tags are
    /// returned unchanged.
    /// </summary>
    public Tag StoreGeneratedTag(Tag tag)
    {
        if (!tag.IsGenerated)
        {
            return tag;
        }

        var dto = CreateChildTagDto(tag, isArchived: false);
        _db.Tags.Add(dto);
        _db.SaveChanges();

        tag.Id = dto.Id;
        tag.IsGenerated = false;
        tag.Description = dto.Description;

        lock (_sync)
        {
            _tagCache.Add(tag);
            RebuildChildTags();
        }

        WeakReferenceMessenger.Default.Send(new TagsChangedMessage());
        WeakReferenceMessenger.Default.Send(new TagApplicationsChangedMessage());
        return tag;
    }

    /// <summary>
    /// Hides a child tag: its subfolder gets no tag any more. A generated tag is stored as archived; a stored one
    /// loses its assignments and is archived. "Reset to generated" (DeleteTagWithChildren) undoes it.
    /// </summary>
    public void HideChildTag(Tag tag)
    {
        if (tag.AutoSourcePath is null)
        {
            return;
        }

        Tag hiddenTag;
        if (tag.IsGenerated)
        {
            var dto = CreateChildTagDto(tag, isArchived: true);
            _db.Tags.Add(dto);
            _db.SaveChanges();
            hiddenTag = new Tag(dto) { Parent = tag.Parent };
        }
        else
        {
            RemoveAssignmentsOfTag(tag.Id!.Value);
            var dto = _db.Tags.First(t => t.Id == tag.Id);
            dto.IsArchived = true;
            dto.UpdatedAtUtc = DateTime.UtcNow;
            _db.SaveChanges();
            hiddenTag = tag;
            hiddenTag.IsArchived = true;
        }

        lock (_sync)
        {
            _tagCache.Remove(tag);
            _hiddenChildTags.Add(hiddenTag);
            RebuildChildTags();
        }

        WeakReferenceMessenger.Default.Send(new TagsChangedMessage());
        WeakReferenceMessenger.Default.Send(new TagApplicationsChangedMessage());
    }

    /// <summary>
    /// Shows a hidden child tag again, with the name and settings it had. Assignments removed when it was hidden
    /// are not restored.
    /// </summary>
    public void UnhideChildTag(int tagId)
    {
        var dto = _db.Tags.FirstOrDefault(t => t.Id == tagId);
        if (dto is null || !IsHiddenChildTag(dto))
        {
            return;
        }

        dto.IsArchived = false;
        dto.UpdatedAtUtc = DateTime.UtcNow;
        _db.SaveChanges();

        lock (_sync)
        {
            var hiddenTag = _hiddenChildTags.FirstOrDefault(t => t.Id == tagId);
            if (hiddenTag is not null)
            {
                _hiddenChildTags.Remove(hiddenTag);
                hiddenTag.IsArchived = false;
                _tagCache.Add(hiddenTag);
            }

            RebuildChildTags();
        }

        WeakReferenceMessenger.Default.Send(new TagsChangedMessage());
        WeakReferenceMessenger.Default.Send(new TagApplicationsChangedMessage());
    }

    private TagDTO CreateChildTagDto(Tag tag, bool isArchived)
    {
        var parentDto = _db.Tags.Include(t => t.Color).First(t => t.Id == tag.Parent!.Id);
        return new TagDTO(tag.Name!, parentDto.Color, description: null, aliases: null, parentDto.IconName)
        {
            ParentId = parentDto.Id,
            AutoSourcePath = tag.AutoSourcePath,
            IsArchived = isArchived,
            CreatedBy = Environment.UserName
        };
    }

    /// <summary>
    /// The ids of the tag and all tags below it (children, their children, ...), parents first.
    /// </summary>
    public IReadOnlyList<int> GetTagAndDescendantIds(int tagId)
    {
        var ids = new List<int> { tagId };
        for (var i = 0; i < ids.Count; i++)
        {
            var parentId = ids[i];
            ids.AddRange(_db.Tags.AsNoTracking().Where(t => t.ParentId == parentId).Select(t => t.Id).ToList());
        }

        return ids;
    }

    /// <summary>
    /// Deletes the tag together with its child tags (the parent foreign key doesn't cascade) and archives all
    /// their assignments. Badges, filters and tag lists update through the messages.
    /// </summary>
    public void DeleteTagWithChildren(int tagId)
    {
        var ids = GetTagAndDescendantIds(tagId);

        // Children first, so no tag is deleted while a child still references it.
        foreach (var id in Enumerable.Reverse(ids))
        {
            RemoveAssignmentsOfTag(id);

            // Assignments whose item isn't in the cache (e.g. a missing file) are archived directly.
            _db.TagAssignments
                .Where(a => (a.TagId == id || a.AutoRuleParentTagId == id) && !a.IsArchived)
                .ExecuteUpdate(setters => setters
                    .SetProperty(a => a.IsArchived, true)
                    .SetProperty(a => a.Enabled, false)
                    .SetProperty(a => a.UpdatedAtUtc, DateTime.UtcNow));

            var dto = _db.Tags.FirstOrDefault(t => t.Id == id);
            if (dto is not null)
            {
                _db.Tags.Remove(dto);
                _db.SaveChanges();
            }
        }

        lock (_sync)
        {
            _tagCache.RemoveAll(t => t.Id is int id && ids.Contains(id));
            _hiddenChildTags.RemoveAll(t => t.Id is int id && ids.Contains(id));

            // A deleted child tag override makes its subfolder's tag generated again.
            RebuildChildTags();
        }

        WeakReferenceMessenger.Default.Send(new TagsChangedMessage());
        WeakReferenceMessenger.Default.Send(new TagApplicationsChangedMessage());
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
        InvalidateInheritedCaches();
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
        InvalidateInheritedCaches();
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
