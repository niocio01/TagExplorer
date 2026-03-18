using CommunityToolkit.Mvvm.ComponentModel;
using TagExplorer.Data;
using TagExplorer.Models;

namespace TagExplorer.ViewModels;

public partial class FilterTag_VM : ObservableObject
{
    [ObservableProperty]
    private FilterTag _tag;

    public int? Id => Tag.Id;
    public string? Name => Tag.Name;
    public Color? Color => Tag.Color;
    public string? Description => Tag.Description;
    public string? IconName => Tag.IconName;
    public FilterTypes FilterType => Tag.FilterType;

    public FilterTag_VM(FilterTag tag)
    {
        Tag = tag;
    }
}
