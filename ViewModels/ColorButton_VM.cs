using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using TagExplorer.Data;
using Color = TagExplorer.Data.Color;

namespace TagExplorer.ViewModels;

public partial class ColorButton_VM : ObservableObject
{
    [ObservableProperty] private Color _color;
    [ObservableProperty] private bool _isSelected;

    public ColorButton_VM() { }

    public ColorButton_VM(Color color)
    {
        Color = color;

        WeakReferenceMessenger.Default.Register<ColorSelected>(this, (r, m) =>
        {
            if (m.Value != Color)
            {
                IsSelected = false;
            }
        });
    }

    [RelayCommand]
    public void SelectColor()
    {
        IsSelected = true;
        WeakReferenceMessenger.Default.Send(new ColorSelected(Color));
    }
}

public class ColorSelected(Color color) : ValueChangedMessage<Color>(color);