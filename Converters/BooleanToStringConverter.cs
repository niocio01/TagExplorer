using System.Globalization;
using System.Windows.Data;

namespace TagExplorer.Converters;

internal class BooleanToString : IValueConverter
{
    public string TrueString { get; set; } = "True";
    public string FalseString { get; set; } = "False";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool && (bool)value ? TrueString : FalseString;
    }

    public object ConvertBack(object value, Type targetType, object parameter,
        CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}