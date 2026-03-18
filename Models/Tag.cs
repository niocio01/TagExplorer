using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel.DataAnnotations;
using TagExplorer.Data;

namespace TagExplorer.Models;

public interface ITag
{
    int? Id { get; set; }
    string? Name { get; set; }
    Color? Color { get; set; }
    string? Description { get; set; }
    string? IconName { get; set; }
    AppliedTag? Parent { get; set; }
    List<AppliedTag>? Children { get; set; }
    List<string>? Aliases { get; set; }
    bool IsSystemTag { get; set; }
}

public partial class AppliedTag : ObservableValidator, ITag
{
    [ObservableProperty]
    private int? _id;

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
    [ObservableProperty] private AppliedTag? _parent;
    [ObservableProperty] private List<AppliedTag>? _children;

    [ObservableProperty] private List<string>? _aliases;
    [ObservableProperty] private bool _isSystemTag = false;
    [ObservableProperty] private bool _isVirtual;
    [ObservableProperty] private TagAssignment? _virtualSourceAssignment;

    public AppliedTag(TagDTO dto)
    {
        Id = dto.Id;
        Name = dto.Name;
        Parent = null;
        Children = [];
        Color = dto.Color;
        Description = dto.Description;
        IconName = dto.IconName;
        Aliases = dto.Aliases;
        IsSystemTag = dto.IsSystemTag;
        IsVirtual = false;
        VirtualSourceAssignment = null;
    }

    public AppliedTag()
    {
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
    public AppliedTag? Parent { get; set; }
    public List<AppliedTag>? Children { get; set; }
    public List<string>? Aliases { get; set; }
    public bool IsSystemTag { get; set; } = false;

    public FilterTag(TagDTO tag)
    {
        Id = tag.Id;
        Name = tag.Name;
        Color = tag.Color;
        Description = tag.Description;
        IconName = tag.IconName;
        Parent = null;
        Children = [];
        Aliases = tag.Aliases;
        IsSystemTag = tag.IsSystemTag;
    }

    public FilterTag()
    {
    }
}