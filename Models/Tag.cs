using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TagExplorer.Data;

namespace TagExplorer.Models;

public partial class Tag : ObservableValidator
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