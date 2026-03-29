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
    private readonly DataCachingService _dcs;

    public TagAssignmentService(AppDbContext db, DataCachingService dcs)
    {
        _db = db;
        _dcs = dcs;
    }

    public IReadOnlyList<Tag> GetManualTagsForItem(ExplorerItem item)
    {
        if (!TryGetAssignmentTarget(item, out var targetPath, out var targetType))
        {
            return [];
        }
        var tags = _dcs.GetTagApplicationsForPath(targetPath);

        if (tags.Count == 0)
        {
            return [];
        }        

        return tags.Select(t => t.Tag).ToList();
    }

    public bool TryAssignManualTag(ExplorerItem item, Tag tag, ApplyScope scope = ApplyScope.Self)
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
            MatchByAlias = true
        });

        _db.SaveChanges();
        return true;
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

    private static bool TryGetAssignmentTarget(ExplorerItem item, out string targetPath, out TargetType targetType)
    {
        targetPath = item.Path;
        switch (item)
        {            
            case Folder:                
                targetType = TargetType.Folder;
                return true;

            case File:
                targetType = TargetType.File;
                return true;

            default:
                targetPath = string.Empty;
                targetType = TargetType.File;
                return false;
        }
    }
}
