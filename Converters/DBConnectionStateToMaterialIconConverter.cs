using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using TagExplorer.ViewModels;
using static TagExplorer.ViewModels.SettingsWindow_VM;

namespace TagExplorer.Converters;

class DBConnectionStateToMaterialIconConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DBConnector.ConnectionState)
        {
            switch (value)
            {
                case DBConnector.ConnectionState.Connected:
                    return "CheckCircleOutline";
                case DBConnector.ConnectionState.Failed:
                    return "AlertCircleOutline";
                case DBConnector.ConnectionState.Unknown:
                    return "HelpCircleOutline";
                default:
                    return "HelpCircleOutline";
            }
        }
        return "HelpCircleOutline";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}