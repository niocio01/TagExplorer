using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TagExplorer.Models;

public enum AssignmentKind
{
    Manual = 0,
    AutoDirectChildrenAsChildTag = 10 // Tag first descendants of a folder with child tags.
}

public enum ApplyScope
{
    Self = 0,
    SelfAndDirectDescendants = 1,
    SelfAndAllDescendants = 2,
    DirectChildren = 3 // Only the direct subfolders, not the folder itself.
}

/// <summary>
/// The ways a tag can be assigned, as offered in the UI. Each maps to a kind and scope of <see cref="TagAssignment"/>.
/// </summary>
public enum AssignmentMode
{
    OnlyThisItem,
    FolderAndContents,
    DirectSubfolders,
    OwnTagPerSubfolder
}

public static class AssignmentModes
{
    public static readonly IReadOnlyList<AssignmentMode> All =
    [
        AssignmentMode.OnlyThisItem,
        AssignmentMode.FolderAndContents,
        AssignmentMode.DirectSubfolders,
        AssignmentMode.OwnTagPerSubfolder
    ];

    /// <summary>
    /// The mode of the assignment, or null for kind/scope combinations the UI doesn't offer.
    /// </summary>
    public static AssignmentMode? Of(TagAssignment assignment) => (assignment.Kind, assignment.Scope) switch
    {
        (AssignmentKind.AutoDirectChildrenAsChildTag, _) => AssignmentMode.OwnTagPerSubfolder,
        (AssignmentKind.Manual, ApplyScope.Self) => AssignmentMode.OnlyThisItem,
        (AssignmentKind.Manual, ApplyScope.SelfAndAllDescendants) => AssignmentMode.FolderAndContents,
        (AssignmentKind.Manual, ApplyScope.DirectChildren) => AssignmentMode.DirectSubfolders,
        _ => null
    };

    public static (AssignmentKind Kind, ApplyScope Scope) ToKindAndScope(AssignmentMode mode) => mode switch
    {
        AssignmentMode.FolderAndContents => (AssignmentKind.Manual, ApplyScope.SelfAndAllDescendants),
        AssignmentMode.DirectSubfolders => (AssignmentKind.Manual, ApplyScope.DirectChildren),
        AssignmentMode.OwnTagPerSubfolder => (AssignmentKind.AutoDirectChildrenAsChildTag, ApplyScope.DirectChildren),
        _ => (AssignmentKind.Manual, ApplyScope.Self)
    };

    public static string DisplayName(AssignmentMode mode) => mode switch
    {
        AssignmentMode.FolderAndContents => "This folder and everything in it",
        AssignmentMode.DirectSubfolders => "Each direct subfolder",
        AssignmentMode.OwnTagPerSubfolder => "Each subfolder gets its own tag",
        _ => "Only this item"
    };

    /// <summary>
    /// True if the assignment tags its target itself; false for rules that only tag items below it.
    /// </summary>
    public static bool AppliesToTarget(TagAssignment assignment) =>
        assignment.Kind == AssignmentKind.Manual && assignment.Scope != ApplyScope.DirectChildren;

    /// <summary>
    /// True if the assignment reaches items other than its target.
    /// </summary>
    public static bool ReachesOtherItems(TagAssignment assignment) =>
        assignment.Kind != AssignmentKind.Manual || assignment.Scope != ApplyScope.Self;

    /// <summary>
    /// The tag shown for the assignment: the assigned tag, or the parent tag of an automatic child-tag rule.
    /// </summary>
    public static int? DisplayTagId(TagAssignment assignment) => assignment.TagId ?? assignment.AutoRuleParentTagId;
}

public enum TargetType
{
    Folder = 0,
    File = 1
}

public sealed class TagAssignment
{
    [Key]
    public int Id { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column(TypeName = "varchar(100)")]
    public string? CreatedBy { get; set; }

    public bool IsArchived { get; set; }

    public AssignmentKind Kind { get; set; }

    public ApplyScope Scope { get; set; }

    public TargetType TargetType { get; set; }

    [Required]
    [MaxLength(2048)]
    [Column(TypeName = "varchar(2048)")]
    public required string TargetPath { get; set; }

    public int? TagId { get; set; }

    /// <summary>
    /// For <see cref="AssignmentKind.AutoDirectChildrenAsChildTag"/>: the tag whose child tags are given to the subfolders.
    /// </summary>
    public int? AutoRuleParentTagId { get; set; }

    public bool MatchByAlias { get; set; } = true;

    public bool Enabled { get; set; } = true;
}
/// <summary>
/// A Tag Application describes a specific usage of a tag on an Explorer Item. It is used to hold references to all involved objects for quick lookup.
/// </summary>
public class TagApplication
{
    public ExplorerItem ExplorerItem { get; init; }
    public Tag Tag { get; init; }
    public TagAssignment Assignment { get; init; }

    /// <summary>
    /// True if the tag reaches this item through an assignment on another item (e.g. a parent folder).
    /// </summary>
    public bool IsVirtual => !string.Equals(ExplorerItem.Path, Assignment.TargetPath, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The path of the item the assignment sits on; for virtual applications the folder the tag comes from.
    /// </summary>
    public string SourcePath => Assignment.TargetPath;

    public TagApplication(ExplorerItem explorerItem, Tag tag, TagAssignment assignment)
    {
        ExplorerItem = explorerItem;
        Tag = tag;
        Assignment = assignment;
    }
}
