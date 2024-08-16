using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TagExplorer.Models;

namespace TagExplorer
{
    public static class Main
    {
        public static ObservableCollection<BaseFolder> BaseFolders = new ObservableCollection<BaseFolder>();
        public static DBConnectionSettings_M DBConnectionSettings = new DBConnectionSettings_M();
        public static TagTable TagTable = new TagTable();

        public static async void InitPre()
        {
            DBConnector.CheckConnection(DBConnectionSettings);
            LoadBaseFolders();
            if (!await TagTable.TableExists())
            {
                TagTable.CreateTable();
            }
        }

        public static void InitPost()
        {
            // Do something
        }

        public static async void LoadBaseFolders()
        {
            if (DBConnector.CurrentConnectionState == DBConnector.ConnectionState.Connected)
            {
                if (!await DBConnector.TableExistsAsync("base_folders"))
                {
                    await DBConnector.CreateBaseFolderTable();
                }
                await DBConnector.LoadBaseFolders();
            }
        }

        
    }
}
