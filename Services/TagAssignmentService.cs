using TagExplorer.Models;
using File = TagExplorer.Models.File;
using Folder = TagExplorer.Models.Folder;

namespace TagExplorer.Services;

public class TagAssignmentService
{
    private readonly DataCachingService _dcs;

    public TagAssignmentService(DataCachingService dcs)
    {
        _dcs = dcs;
    }

    /// <summary>
    /// Assigns the tag to the item in the given mode. Modes other than <see cref="AssignmentMode.OnlyThisItem"/>
    /// need a folder. For <see cref="AssignmentMode.OwnTagPerSubfolder"/> the tag is the parent of the child tags.
    /// </summary>
    /// <returns>False if nothing was assigned (invalid target or mode, or the same assignment exists).</returns>
    public bool AssignTag(ExplorerItem? item, Tag tag, AssignmentMode mode = AssignmentMode.OnlyThisItem)
    {
        if (item is null || tag.Id is null)
        {
            return false;
        }

        if (!TryGetAssignmentTarget(item, out var targetPath, out var targetType))
        {
            return false;
        }

        return AddAssignment(targetPath, targetType, tag.Id.Value, mode, item);
    }

    /// <summary>
    /// Replaces the assignment with one in the new mode, on the same item and with the same tag.
    /// </summary>
    /// <param name="targetItem">The item the assignment sits on, if known; needed for items outside the cache.</param>
    public bool ChangeMode(TagAssignment assignment, AssignmentMode mode, ExplorerItem? targetItem = null)
    {
        if (AssignmentModes.Of(assignment) == mode || AssignmentModes.DisplayTagId(assignment) is not int tagId)
        {
            return false;
        }

        // AddAssignment replaces the assignment, since an item has each tag only once.
        return AddAssignment(assignment.TargetPath, assignment.TargetType, tagId, mode, targetItem);
    }

    public void RemoveAssignment(TagAssignment assignment)
    {
        _dcs.RemoveAssignment(assignment);
    }

    /// <summary>
    /// The assignments that assigning the tag to the item in the given mode would replace (same tag, other kind).
    /// </summary>
    public IReadOnlyList<TagAssignment> GetAssignmentsReplacedBy(ExplorerItem item, Tag tag, AssignmentMode mode)
    {
        if (tag.Id is null || !TryGetAssignmentTarget(item, out var targetPath, out var targetType))
        {
            return [];
        }

        return GetAssignmentsOfTagOnItem(targetPath, targetType, tag.Id.Value)
            .Where(a => AssignmentModes.Of(a) != mode)
            .ToList();
    }

    private List<TagAssignment> GetAssignmentsOfTagOnItem(string targetPath, TargetType targetType, int tagId)
    {
        return _dcs.TagAssignments
            .Where(a => a.Enabled
                && !a.IsArchived
                && a.TargetType == targetType
                && string.Equals(a.TargetPath, targetPath, StringComparison.OrdinalIgnoreCase)
                && AssignmentModes.DisplayTagId(a) == tagId)
            .ToList();
    }

    /// <summary>
    /// Saves a new assignment, replacing assignments of the same tag on the same item in another kind.
    /// </summary>
    private bool AddAssignment(string targetPath, TargetType targetType, int tagId, AssignmentMode mode, ExplorerItem? targetItem)
    {
        if (mode != AssignmentMode.OnlyThisItem && targetType != TargetType.Folder)
        {
            return false;
        }

        var (kind, scope) = AssignmentModes.ToKindAndScope(mode);
        var isChildTagRule = kind == AssignmentKind.AutoDirectChildrenAsChildTag;

        var assignmentsOfTagOnItem = GetAssignmentsOfTagOnItem(targetPath, targetType, tagId);

        if (assignmentsOfTagOnItem.Any(a => a.Kind == kind && a.Scope == scope))
        {
            return false;
        }

        // An item has each tag only once, so assigning it in another kind replaces the old assignment.
        foreach (var oldAssignment in assignmentsOfTagOnItem)
        {
            _dcs.RemoveAssignment(oldAssignment);
        }

        _dcs.AddAssignment(new TagAssignment
        {
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            CreatedBy = Environment.UserName,
            IsArchived = false,
            Enabled = true,
            Kind = kind,
            Scope = scope,
            TargetType = targetType,
            TargetPath = targetPath,
            TagId = isChildTagRule ? null : tagId,
            AutoRuleParentTagId = isChildTagRule ? tagId : null,
            MatchByAlias = true
        }, targetItem);

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
