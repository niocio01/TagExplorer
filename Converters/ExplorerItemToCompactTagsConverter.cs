using System.Globalization;
using System.Windows.Data;
using TagExplorer.Models;
using TagExplorer.ViewModels;

namespace TagExplorer.Converters;

internal class ExplorerItemToCompactTagsConverter : IMultiValueConverter
{
    public int MaxVisible { get; set; } = 3;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not ExplorerItem item || values[1] is not FileList_VM vm)
        {
            return Array.Empty<CompactTagToken>();
        }

        var maxVisible = MaxVisible;
        if (parameter is string parameterString && int.TryParse(parameterString, out var parsedMaxVisible) && parsedMaxVisible > 0)
        {
            maxVisible = parsedMaxVisible;
        }

        return vm.GetCompactTagTokens(item, maxVisible);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
