using CommunityToolkit.Mvvm.ComponentModel;
using TagExplorer.Data;
using TagExplorer.Models;

namespace TagExplorer.ViewModels;

public partial class AppliedTag_VM : ObservableObject
{
    [ObservableProperty]
    private Tag _tag;

    public int? Id => Tag.Id;
    public string? Name => Tag.Name;
    public Color? Color => Tag.Color;
    public string? Description => Tag.Description;
    public string? IconName => Tag.IconName;

    public AppliedTag_VM(Tag tag)
    {
        Tag = tag;
    }
}
