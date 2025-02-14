using System.Globalization;
using System.Windows.Data;

namespace TagExplorer.Converters;

internal class BooleanToIconNameConverter : IValueConverter
{
    public string TrueIconName { get; set; } = "AlertCircle";
    public string FalseIconName { get; set; } = "AlertCircle";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool && (bool)value ? TrueIconName : FalseIconName;
    }

    public object ConvertBack(object value, Type targetType, object parameter,
        CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}