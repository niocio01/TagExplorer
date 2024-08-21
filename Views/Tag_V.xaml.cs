using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using CommunityToolkit.Mvvm.ComponentModel;
using TagExplorer.Models;
using TagExplorer.ViewModels;

namespace TagExplorer.Views
{
    /// <summary>
    /// Interaction logic for Tag_V.xaml
    /// </summary>
    public partial class Tag_V
    {
        public Tag Tag;

        public Tag_V()
        {
            InitializeComponent();
        }
    }
}
