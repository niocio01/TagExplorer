using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TagExplorer.ViewModels;
using TagExplorer.Views;

namespace TagExplorer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        MainWindow_VM ViewModel;
        SettingsWindow_V SettingsWindow;
        public MainWindow()
        {
            InitializeComponent();

            ViewModel = new MainWindow_VM();
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