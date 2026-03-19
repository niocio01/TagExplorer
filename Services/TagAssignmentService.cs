using Microsoft.EntityFrameworkCore;
using System.IO;
using TagExplorer.Data;
using TagExplorer.Models;
using File = TagExplorer.Models.File;
using Folder = TagExplorer.Models.Folder;

namespace TagExplorer.Services;

public class TagAssignmentService
{
    private readonly AppDbContext _db;

    public TagAssignmentService(AppDbContext db)
    {
        _db = db;
    }

    public IReadOnlyList<FilterTag> GetManualTagsForItem(ExplorerItem? item)
    {
        if (!TryGetAssignmentTarget(item, out var targetPath, out var targetType))
        {
            return [];
        }

        var manualTagIds = _db.TagAssignments
            .Where(a => a.Enabled
                        && !a.IsArchived
                        && a.Kind == AssignmentKind.Manual
                        && a.TargetType == targetType
                        && a.TargetPath == targetPath
                        && a.TagId.HasValue)
            .Select(a => a.TagId!.Value)
            .Distinct()
            .ToList();

        if (manualTagIds.Count == 0)
        {
            return [];
        }

        var tags = _db.Tags
            .Include(t => t.Color)
            .Where(t => manualTagIds.Contains(t.Id) && !t.IsArchived)
            .ToList();

        return tags.Select(t => new FilterTag(t)).ToList();
    }

    public IReadOnlyDictionary<int, CompactTagDefinition> GetCompactTagDefinitionsByIds(IEnumerable<int> tagIds)
    {
        var ids = tagIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, CompactTagDefinition>();
        }

        var tags = _db.Tags
            .Include(t => t.Color)
            .Where(t => ids.Contains(t.Id) && !t.IsArchived)
            .ToList();

        return tags.ToDictionary(
            t => t.Id,
            t => new CompactTagDefinition
            {
                Id = t.Id,
                Name = t.Name,
                ShortCode = t.ShortCode,
                IconName = t.IconName,
                ColorHex = t.Color?.HexCode
            });
    }

    public bool TryAssignManualTag(ExplorerItem? item, FilterTag tag, ApplyScope scope = ApplyScope.Self)
    {
        if (tag.Id is null)
        {
            return false;
        }

        if (!TryGetAssignmentTarget(item, out var targetPath, out var targetType))
        {
            return false;
        }

        var tagId = tag.Id.Value;

        var alreadyAssigned = _db.TagAssignments.Any(a =>
            a.Enabled
            && !a.IsArchived
            && a.Kind == AssignmentKind.Manual
            && a.TargetType == targetType
            && a.TargetPath == targetPath
            && a.Scope == scope
            && a.TagId == tagId);

        if (alreadyAssigned)
        {
            return false;
        }

        _db.TagAssignments.Add(new TagAssignment
        {
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CreatedBy = Environment.UserName,
            IsArchived = false,
            Enabled = true,
            Kind = AssignmentKind.Manual,
            Scope = scope,
            TargetType = targetType,
            TargetPath = targetPath,
            TagId = tagId,
            AutoRuleParentTagId = null,
            MatchByAlias = true
        });

        _db.SaveChanges();
        return true;
    }

    public IReadOnlyDictionary<string, HashSet<int>> GetEffectiveTagIdsByPath(IEnumerable<ExplorerItem> items, string? currentRootPath = null)
    {
        var itemInfos = new List<(string Path, TargetType Type)>();
        var itemPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (TryGetAssignmentTarget(item, out var targetPath, out var targetType))
            {
                itemInfos.Add((targetPath, targetType));
                itemPaths.Add(targetPath);
            }
        }

        if (itemInfos.Count == 0)
        {
            return new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        }

        var normalizedRootPath = string.IsNullOrWhiteSpace(currentRootPath)
            ? null
            : PathNormalizer.NormalizeAbsolutePath(currentRootPath);

        var pathSeparator = Path.DirectorySeparatorChar.ToString();

        var directAssignments = _db.TagAssignments
            .Where(a => a.Enabled
                        && !a.IsArchived
                        && a.Kind == AssignmentKind.Manual
                        && a.TagId.HasValue
                        && itemPaths.Contains(a.TargetPath))
            .Select(a => new { a.TargetPath, a.TargetType, a.Scope, TagId = a.TagId!.Value })
            .ToList();

        var inheritedFolderAssignments = _db.TagAssignments
            .Where(a => a.Enabled
                        && !a.IsArchived
                        && a.Kind == AssignmentKind.Manual
                        && a.TargetType == TargetType.Folder
                        && a.TagId.HasValue
                        && a.Scope != ApplyScope.Self)
            .Where(a => normalizedRootPath == null
                || a.TargetPath == normalizedRootPath
                || a.TargetPath.StartsWith(normalizedRootPath + pathSeparator)
                || normalizedRootPath.StartsWith(a.TargetPath + pathSeparator))
            .Select(a => new { a.TargetPath, a.Scope, TagId = a.TagId!.Value })
            .ToList();

        var result = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        foreach (var assignment in directAssignments)
        {
            foreach (var item in itemInfos)
            {
                if (!string.Equals(item.Path, assignment.TargetPath, StringComparison.OrdinalIgnoreCase)
                    || item.Type != assignment.TargetType)
                {
                    continue;
                }

                AddTagId(result, item.Path, assignment.TagId);
            }
        }

        foreach (var assignment in inheritedFolderAssignments)
        {
            foreach (var item in itemInfos)
            {
                if (!IsDescendantOrSelf(item.Path, assignment.TargetPath))
                {
                    continue;
                }

                if (assignment.Scope == ApplyScope.SelfAndDirectDescendants)
                {
                    var relativeDepth = GetRelativeDepth(assignment.TargetPath, item.Path);
                    if (relativeDepth > 1)
                    {
                        continue;
                    }
                }

                AddTagId(result, item.Path, assignment.TagId);
            }
        }

        return result;
    }

    private static void AddTagId(IDictionary<string, HashSet<int>> map, string path, int tagId)
    {
        if (!map.TryGetValue(path, out var tagIds))
        {
            tagIds = [];
            map[path] = tagIds;
        }

        tagIds.Add(tagId);
    }

    private static bool IsDescendantOrSelf(string itemPath, string ancestorPath)
    {
        if (string.Equals(itemPath, ancestorPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return itemPath.StartsWith(ancestorPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetRelativeDepth(string ancestorPath, string descendantPath)
    {
        if (!IsDescendantOrSelf(descendantPath, ancestorPath))
        {
            return int.MaxValue;
        }

        if (string.Equals(ancestorPath, descendantPath, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var relative = descendantPath[ancestorPath.Length..]
            .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (string.IsNullOrWhiteSpace(relative))
        {
            return 0;
        }

        return relative.Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries).Length;
    }

    public bool RemoveManualTag(ExplorerItem? item, FilterTag tag)
    {
        if (tag.Id is null)
        {
            return false;
        }

        if (!TryGetAssignmentTarget(item, out var targetPath, out var targetType))
        {
            return false;
        }

        var tagId = tag.Id.Value;

        var assignments = _db.TagAssignments
            .Where(a => a.Enabled
                        && !a.IsArchived
                        && a.Kind == AssignmentKind.Manual
                        && a.TargetType == targetType
                        && a.TargetPath == targetPath
                        && a.TagId == tagId)
            .ToList();

        if (assignments.Count == 0)
        {
            return false;
        }

        foreach (var assignment in assignments)
        {
            assignment.Enabled = false;
            assignment.IsArchived = true;
            assignment.UpdatedAtUtc = DateTime.UtcNow;
        }

        _db.SaveChanges();
        return true;
    }

    private static bool TryGetAssignmentTarget(ExplorerItem? item, out string targetPath, out TargetType targetType)
    {
        switch (item)
        {
            case Folder folder:
                targetPath = PathNormalizer.NormalizeAbsolutePath(folder.Path);
                targetType = TargetType.Folder;
                return true;

            case File file when !string.IsNullOrWhiteSpace(file.FullPath):
                targetPath = PathNormalizer.NormalizeAbsolutePath(file.FullPath);
                targetType = TargetType.File;
                return true;

            default:
                targetPath = string.Empty;
                targetType = TargetType.File;
                return false;
        }
    }
}
