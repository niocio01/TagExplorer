using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using TagExplorer.Data;

namespace TagExplorer.ViewModels;

public partial class Tag_VM : ObservableObject
{
    public event EventHandler TagPressed;

    [ObservableProperty]
    private TagDTO _tag;
    [ObservableProperty] private bool _isSelected;

    public string Name => Tag.Name;
    // Shown as the chip's tooltip: for hidden or orphaned subfolder tags it says why they look different.
    public string? Description => ToolTipText;
    public string IconName => Tag.IconName;
    public Color Color => Tag.Color;
    public bool IsGenerated => Tag.IsGenerated;

    /// <summary>A child tag of a child-tag rule: generated, customised (stored) or hidden.</summary>
    public bool IsSubfolderTag => Tag.IsGenerated || Tag.AutoSourcePath is not null;

    /// <summary>A stored subfolder tag that hides the generated tag of its subfolder.</summary>
    public bool IsHidden => Tag.AutoSourcePath is not null && Tag.IsArchived;

    /// <summary>A stored subfolder tag whose subfolder no active rule covers (folder renamed or deleted, rule removed).</summary>
    [ObservableProperty]
    private bool _isOrphaned;

    public string? ToolTipText => IsHidden
        ? $"Hidden: {Tag.AutoSourcePath} gets no tag. Select it and press the eye button to show it again."
        : IsOrphaned ? $"Folder not found: {Tag.AutoSourcePath}" : Tag.Description;



    public Tag_VM() { }

    public Tag_VM(TagDTO tag)
    {
        Tag = tag;
    }

    [RelayCommand]
    public void UpdateProps()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Color));
        OnPropertyChanged(nameof(Description));
        OnPropertyChanged(nameof(IconName));
        OnPropertyChanged(nameof(ToolTipText));
    }

    partial void OnIsOrphanedChanged(bool value)
    {
        OnPropertyChanged(nameof(ToolTipText));
        OnPropertyChanged(nameof(Description));
    }

    /// <summary>
    /// A view model for a generated (runtime-only) child tag, backed by an untracked DTO.
    /// </summary>
    public static Tag_VM ForGenerated(Models.Tag tag)
    {
        return new Tag_VM(new TagDTO
        {
            Id = tag.Id!.Value,
            Name = tag.Name ?? string.Empty,
            Color = tag.Color!,
            ColorId = tag.Color?.Id ?? 0,
            IconName = tag.IconName ?? string.Empty,
            Description = tag.Description,
            ParentId = tag.Parent?.Id,
            AutoSourcePath = tag.AutoSourcePath,
            IsGenerated = true
        });
    }

    [RelayCommand]
    public void PressTag()
    {
        TagPressed?.Invoke(this, EventArgs.Empty);
    }
}

