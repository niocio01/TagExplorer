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
    SelfAndAllDescendants = 2
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

    public int? AutoRuleParentTagId { get; set; }

    public bool MatchByAlias { get; set; } = true;

    public bool Enabled { get; set; } = true;

    public bool IsValid()
    {
        return Kind switch
        {
            AssignmentKind.Manual =>
                TagId.HasValue &&
                AutoRuleParentTagId is null,

            AssignmentKind.AutoDirectChildrenAsChildTag =>
                TargetType == TargetType.Folder &&
                TagId is null &&
                AutoRuleParentTagId.HasValue,

            _ => false
        };
    }
}
