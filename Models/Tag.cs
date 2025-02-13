using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel.DataAnnotations.Schema;
using TagExplorer.Data;

namespace TagExplorer.Models;

public partial class Tag : ObservableObject
{
    public readonly TagDTO DTO;
    [ObservableProperty] private string _name;
    [ObservableProperty] private Tag? _parent;
    [ObservableProperty] private List<Tag> _children;
    [ObservableProperty] private Color _color;
    [ObservableProperty] private string _description;
    [ObservableProperty] private string _iconName;
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
}