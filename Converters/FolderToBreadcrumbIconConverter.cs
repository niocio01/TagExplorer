using System.Globalization;
using System.Windows.Data;
using TagExplorer.Models;

namespace TagExplorer.Converters;

internal class FolderToBreadcrumbIconConverter : IValueConverter
{
    public string BaseFolderIcon { get; set; } = "HomeOutline";
    public string FolderIcon { get; set; } = "ChevronRight";

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Folder folder)
        {
            return folder.ParentFolder == null ? BaseFolderIcon : FolderIcon;
        }

        return "help";
    }

    public object ConvertBack(object value, Type targetType, object parameter,
        CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}