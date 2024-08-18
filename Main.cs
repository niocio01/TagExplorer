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
        public static DBConnectionSettings_M DBConnectionSettings = new DBConnectionSettings_M();
        public static BaseFolderTable BaseFolderTable = new BaseFolderTable();
        public static ColorTable ColorTable = new ColorTable();
        public static TagTable TagTable = new TagTable();

        public static async void InitPre()
        {
            DBConnector.CheckConnection(DBConnectionSettings);
            if (DBConnector.CurrentConnectionState != DBConnector.ConnectionState.Connected) return;
            if (!await BaseFolderTable.TableExists())
            {
                BaseFolderTable.CreateTable();
            }

            BaseFolderTable.GetList();

            if (!await ColorTable.TableExists())
            {
                ColorTable.CreateTable();
                DefaultColors.AddDefaultColors(ColorTable);
            }
            else
            {
                //ColorTable.GetList();
            }

            if (!await TagTable.TableExists())
            {
                TagTable.CreateTable();
                SystemTags.AddSystemTags();
            }

            TagTable.GetList();
            GetMissingTableData();
        }

        public static void GetMissingTableData()
        {
            while (!AllDataIsFilled())
            {
                BaseFolderTable.GetMissingData();
                ColorTable.GetMissingData();
                TagTable.GetMissingData();
            }
        }

        public static bool AllDataIsFilled()
        {
            return BaseFolderTable.DataIsFilled && ColorTable.DataIsFilled && TagTable.DataIsFilled;
        }

        public static void InitPost()
        {
            // Do something
        }
    }
}