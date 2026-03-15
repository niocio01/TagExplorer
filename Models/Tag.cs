using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel.DataAnnotations;
using TagExplorer.Data;

namespace TagExplorer.Models;

public interface ITag
{
    string? Name { get; set; }
    Color? Color { get; set; }
    string? Description { get; set; }
    string? IconName { get; set; }
    Tag? Parent { get; set; }
    List<Tag>? Children { get; set; }
    List<string>? Aliases { get; set; }
    bool IsSystemTag { get; set; }
}

public partial class Tag : ObservableValidator, ITag
{
    public readonly TagDTO? DTO;

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
    [ObservableProperty] private Tag? _parent;
    [ObservableProperty] private List<Tag>? _children;

    [ObservableProperty] private List<string>? _aliases;
    [ObservableProperty] private bool _isSystemTag;

    public Tag(TagDTO dto)
    {
        DTO = dto;
        Name = dto.Name;
        Parent = null;
        Children = [];
        Color = dto.Color;
        Description = dto.Description;
        IconName = dto.IconName;
        Aliases = dto.Aliases;
        IsSystemTag = dto.IsSystemTag;
    }

    public Tag()
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

    public string? Name { get; set; }
    public Color? Color { get; set; }
    public string? Description { get; set; }
    public string? IconName { get; set; }
    public Tag? Parent { get; set; }
    public List<Tag>? Children { get; set; }
    public List<string>? Aliases { get; set; }
    public bool IsSystemTag { get; set; }

    public FilterTag(TagDTO tag)
    {
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