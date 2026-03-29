using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
        Parent = dto.Parent is null ? null : new FilterTag(dto.Parent);
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

public partial class FilterTag : Filter,  ITag
{
    public int? Id { get; set; }
    public string? Name { get; set; }
    public Color? Color { get; set; }
    public string? Description { get; set; }
    public string? IconName { get; set; }
    public string? ShortCode { get; set; }
    public List<string>? Aliases { get; set; }
    public bool IsSystemTag { get; set; } = false;

    public FilterTag(TagDTO tag)
    {
        Id = tag.Id;
        Name = tag.Name;
        Color = tag.Color;
        Description = tag.Description;
        IconName = tag.IconName;
        ShortCode = tag.ShortCode;
        Aliases = tag.Aliases;
        IsSystemTag = tag.IsSystemTag;
    }
}