using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;
using TagExplorer.Data;

namespace TagExplorer.Models;

public interface ITag
{
    int? Id { get; }
    string? Name { get; }
    Color? Color { get; }
    string? Description { get; }
    string? IconName { get; }
    string? ShortCode { get; }
    List<string>? Aliases { get; }
    bool IsSystemTag { get; }
}

public partial class Tag : ObservableValidator, ITag
{
    [ObservableProperty]
    private int? _id;

    [ObservableProperty]
    private DateTime _createdAtUtc = DateTime.UtcNow;

    [ObservableProperty]
    private DateTime _updatedAtUtc = DateTime.UtcNow;

    [ObservableProperty]
    private string? _createdBy;

    [ObservableProperty]
    private bool _isArchived;

    [ObservableProperty]
    [Required]
    [StringLength(50)]
    private string? _name;

    [ObservableProperty]
    [Required]
    private Color? _color;

    [ObservableProperty]
    [StringLength(200)]
    private string? _description;

    [ObservableProperty] private string? _iconName;
    [ObservableProperty] private string? _shortCode;
    [ObservableProperty] private ITag? _parent;
    [ObservableProperty] private List<ITag>? _children;

    [ObservableProperty] private List<string>? _aliases;
    [ObservableProperty] private bool _isSystemTag = false;

    public Tag(TagDTO dto)
    {
        Id = dto.Id;
        CreatedAtUtc = dto.CreatedAtUtc;
        UpdatedAtUtc = dto.UpdatedAtUtc;
        CreatedBy = dto.CreatedBy;
        IsArchived = dto.IsArchived;
        Name = dto.Name;
        Parent = dto.Parent is null ? null : new Tag(dto.Parent);
        Children = [];
        Color = dto.Color;
        Description = dto.Description;
        IconName = dto.IconName;
        ShortCode = dto.ShortCode;
        Aliases = dto.Aliases;
        IsSystemTag = dto.IsSystemTag;
    }

    /// <summary>
    /// Copies the editable values of a saved tag, so objects already referencing this tag show the changes.
    /// </summary>
    public void UpdateFrom(TagDTO dto)
    {
        UpdatedAtUtc = dto.UpdatedAtUtc;
        Name = dto.Name;
        Color = dto.Color;
        Description = dto.Description;
        IconName = dto.IconName;
        ShortCode = dto.ShortCode;
        Aliases = dto.Aliases;
        IsSystemTag = dto.IsSystemTag;
    }

    public bool IsValid()
    {
        ValidateAllProperties();
        return !HasErrors;
    }
}

public partial class FilterTag : Filter, ITag
{
    public Tag Tag { get; init; }

    public int? Id => Tag.Id;
    public string? Name => Tag.Name;
    public Color? Color => Tag.Color;
    public string? Description => Tag.Description;
    public string? IconName => Tag.IconName;
    public string? ShortCode => Tag.ShortCode;
    public List<string>? Aliases => Tag.Aliases;
    public bool IsSystemTag => Tag.IsSystemTag;

    public FilterTag(Tag tag)
    {
        Tag = tag;
        // The chip binds to the forwarding properties above, so pass on the tag's changes.
        Tag.PropertyChanged += (_, e) => OnPropertyChanged(e.PropertyName);
    }
}

public enum AppliedTagKind
{
    /// <summary>Assigned to the item itself.</summary>
    Direct,

    /// <summary>Reaches the item through an assignment on a folder above it.</summary>
    Virtual,

    /// <summary>A rule on the folder that only tags items below it (its subfolders).</summary>
    Rule
}

/// <summary>
/// A tag shown on the selected item in the details pane, with the actions of its context menu.
/// </summary>
public partial class AppliedTag : ITag
{
    public required Tag Tag { get; init; }
    public required TagAssignment Assignment { get; init; }
    public required AppliedTagKind Kind { get; init; }

    public bool IsVirtual => Kind == AppliedTagKind.Virtual;
    public bool IsRule => Kind == AppliedTagKind.Rule;
    public bool CanRemove => Kind != AppliedTagKind.Virtual;

    /// <summary>
    /// The folder the assignment sits on (for virtual tags, the folder the tag comes from).
    /// </summary>
    public string SourcePath => Assignment.TargetPath;
    public string SourceName => System.IO.Path.GetFileName(SourcePath) is { Length: > 0 } name ? name : SourcePath;
    public string GoToSourceHeader => $"Go to \"{SourceName}\"";
    public string RemoveFromSourceHeader => $"Remove from \"{SourceName}\"";

    public string ToolTipText => Kind switch
    {
        AppliedTagKind.Virtual => $"From {SourcePath}",
        AppliedTagKind.Rule => $"{ModeDisplayName}: tags the subfolders, not this folder",
        _ => Tag.Description ?? Tag.Name ?? string.Empty
    };

    private string ModeDisplayName => AssignmentModes.Of(Assignment) is AssignmentMode mode
        ? AssignmentModes.DisplayName(mode)
        : string.Empty;

    /// <summary>
    /// The kinds the assignment can be changed to; empty if it can't be changed (files, virtual tags).
    /// </summary>
    public IReadOnlyList<AssignmentModeOption> ModeOptions { get; init; } = [];
    public bool CanChangeMode => ModeOptions.Count > 0;

    public System.Windows.Input.ICommand? GoToSourceCommand { get; init; }
    public System.Windows.Input.ICommand? RemoveFromSourceCommand { get; init; }

    public int? Id => Tag.Id;
    public string? Name => Tag.Name;
    public Color? Color => Tag.Color;

    // The chip shows the description as its tooltip; for virtual tags and rules it says where the tag comes from.
    public string? Description => Kind == AppliedTagKind.Direct ? Tag.Description : ToolTipText;
    public string? IconName => Tag.IconName;
    public string? ShortCode => Tag.ShortCode;
    public List<string>? Aliases => Tag.Aliases;
    public bool IsSystemTag => Tag.IsSystemTag;

}

/// <summary>
/// One entry of the "Kind" menu of an applied tag.
/// </summary>
public sealed record AssignmentModeOption(string Header, bool IsCurrent, System.Windows.Input.ICommand Command);

/// <summary>
/// A tag dropped with Shift held and a kind picked from the menu.
/// </summary>
public sealed record TagDropRequest(object Payload, AssignmentMode Mode);