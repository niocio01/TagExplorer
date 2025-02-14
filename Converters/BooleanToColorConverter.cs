using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace TagExplorer.Converters;

internal class BooleanToColorConverter : IValueConverter
{
    public SolidColorBrush TrueColor { get; set; }
    public SolidColorBrush FalseColor { get; set; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool && (bool)value ? TrueColor : FalseColor;
    }

    public object ConvertBack(object value, Type targetType, object parameter,
        CultureInfo culture)
    {
        if (value is Visibility && (Visibility)value == Visibility.Visible) return true;

        return false;
    }
}