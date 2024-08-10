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
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TagExplorer.ViewModels;

namespace TagExplorer.Views
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class SettingsWindow_V : Window
    {
        private readonly SettingsWindow_VM ViewModel;

        public SettingsWindow_V()
        {
            InitializeComponent();

            ViewModel = new SettingsWindow_VM(this);
            DataContext = ViewModel;
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
            SetDarkStatusbar.UseImmersiveDarkMode(source.Handle, true);
        }
    }
}
