using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using TagExplorer.Views;

namespace TagExplorer.Models
{
    public class DBConnectionSettings_M
    {
        public String? Host { get; set; }

        public String? Port { get; set; }

        public String? Username { get; set; }

        public String? Password { get; set; }

        public DBConnectionSettings_M()
        {
            Host = Properties.Settings.Default.Host;
            Port = Properties.Settings.Default.Port;
            Username = Properties.Settings.Default.Username;
            Password = Properties.Settings.Default.Password;
        }
    }
}
