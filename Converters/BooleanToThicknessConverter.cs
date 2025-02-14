using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TagExplorer.Converters;

internal class BooleanToThicknessConverter : IValueConverter
{
    public Thickness TrueThickness { get; set; } = new(3);
    public Thickness FalseThickness { get; set; } = new(0);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool && (bool)value) return TrueThickness;

        return FalseThickness;
    }

    public object ConvertBack(object value, Type targetType, object parameter,
        CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}