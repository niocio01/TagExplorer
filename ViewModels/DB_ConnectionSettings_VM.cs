using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using TagExplorer.Models;

namespace TagExplorer.ViewModels
{
    public partial class DBConnectionSettings_VM : ObservableObject
    {
        [ObservableProperty]
        private DBConnectionSettings_M _DBConnectionSettings;

        public DBConnectionSettings_VM(DBConnectionSettings_M dbConnectionSettingsM)
        {
            DBConnectionSettings = dbConnectionSettingsM;
        }

        
    }
}
