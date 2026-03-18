using CommunityToolkit.Mvvm.ComponentModel;
using TagExplorer.Data;
using TagExplorer.Models;

namespace TagExplorer.ViewModels;

public partial class AppliedTag_VM : ObservableObject
{
    [ObservableProperty]
    private AppliedTag _tag;

    public int? Id => Tag.Id;
    public string? Name => Tag.Name;
    public Color? Color => Tag.Color;
    public string? Description => Tag.Description;
    public string? IconName => Tag.IconName;
    public bool IsVirtual => Tag.IsVirtual;
    public TagAssignment? VirtualSourceAssignment => Tag.VirtualSourceAssignment;

    public AppliedTag_VM(AppliedTag tag)
    {
        Tag = tag;
    }
}
