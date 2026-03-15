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
    public string? Description => Tag.Description;
    public string IconName => Tag.IconName;
    public Color Color => Tag.Color;



    public Tag_VM() {}

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
    }

    [RelayCommand]
    public void PressTag()
    {
        TagPressed?.Invoke(this, EventArgs.Empty);
    }
}

