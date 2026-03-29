using System.Windows;
using System.Windows.Interop;

namespace TagExplorer.Views
{
    /// <summary>
    /// Interaction logic for Settings.xaml
    /// </summary>
    public partial class SettingsWindow_V : Window
    {
        public SettingsWindow_V()
        {
            InitializeComponent();
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
            SetDarkStatusbar.UseImmersiveDarkMode(source.Handle, true);
        }
    }
}
