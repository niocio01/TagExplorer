using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using TagExplorer.Models;

namespace TagExplorer.ViewModels
{
    public partial class TabsOverview_VM : ObservableObject
    {
        [ObservableProperty] private TagTable _tagTable = Main.TagTable;

        [ObservableProperty] private Tag _selectedTag;

        public TabsOverview_VM()
        {
            SelectedTag = TagTable.Data.FirstOrDefault();
        }
    }
}