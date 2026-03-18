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
    public int Id { get; set; }

    public AssignmentKind Kind { get; set; }

    public ApplyScope Scope { get; set; }

    public TargetType TargetType { get; set; }

    public required string TargetPath { get; set; }

    public int? TagId { get; set; }

    public int? ParentTagId { get; set; }

    public bool MatchByAlias { get; set; } = true;

    public bool Enabled { get; set; } = true;

    public bool IsValid()
    {
        return Kind switch
        {
            AssignmentKind.Manual =>
                TagId.HasValue &&
                ParentTagId is null,

            AssignmentKind.AutoDirectChildrenAsChildTag =>
                TargetType == TargetType.Folder &&
                TagId is null &&
                ParentTagId.HasValue,

            _ => false
        };
    }
}
