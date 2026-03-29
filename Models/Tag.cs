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
    }
}

public partial class AppliedTag : ITag
{
    public TagApplication? Application { get; init; }
    public Tag Tag => Application.Tag;
    public int? Id => Tag.Id;
    public string? Name => Tag.Name;
    public Color? Color => Tag.Color;
    public string? Description => Tag.Description;
    public string? IconName => Tag.IconName;
    public string? ShortCode => Tag.ShortCode;
    public List<string>? Aliases => Tag.Aliases;
    public bool IsSystemTag => Tag.IsSystemTag;

    public AppliedTag(TagApplication tagApplication)
    {
        Application = tagApplication;
    }
}