using System.Globalization;
using System.Windows;
using System.Windows.Data;
using TagExplorer.Models;
using File = TagExplorer.Models.File;
using Folder = TagExplorer.Models.Folder;

namespace TagExplorer.Converters;

internal class ExplorerItemToCompactIndentConverter : IMultiValueConverter
{
    public Thickness NestedItemMargin { get; set; } = new(16, 0, 0, 0);
    public Thickness DirectItemMargin { get; set; } = new(0);

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not ExplorerItem item)
        {
            return DirectItemMargin;
        }

        var currentFolderPath = values[1] as string;
        if (string.IsNullOrWhiteSpace(currentFolderPath))
        {
            return DirectItemMargin;
        }

        var itemPath = GetItemPath(item);
        if (string.IsNullOrWhiteSpace(itemPath))
        {
            return DirectItemMargin;
        }

        var depth = GetDepth(currentFolderPath, itemPath);
        return depth > 1 ? NestedItemMargin : DirectItemMargin;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static string? GetItemPath(ExplorerItem item)
    {
        return item switch
        {
            Folder folder => folder.Path,
            File file => file.FullPath,
            _ => null
        };
    }

    private static int GetDepth(string rootPath, string currentPath)
    {
        if (currentPath.Length <= rootPath.Length)
        {
            return 0;
        }

        if (!currentPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        var relative = currentPath[rootPath.Length..]
            .TrimStart(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);

        if (string.IsNullOrWhiteSpace(relative))
        {
            return 0;
        }

        return relative.Split([System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
