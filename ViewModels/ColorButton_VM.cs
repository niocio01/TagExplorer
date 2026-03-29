using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Color = TagExplorer.Data.Color;

namespace TagExplorer.ViewModels;

public partial class ColorButton_VM : ObservableObject
{
    public event EventHandler ColorPressed;

    [ObservableProperty] private Color _color;
    [ObservableProperty] private bool _isSelected;

    public ColorButton_VM() { }

    public ColorButton_VM(Color color)
    {
        Color = color;
    }

    [RelayCommand]
    public void SelectColor()
    {
        ColorPressed?.Invoke(this, EventArgs.Empty);
    }
}