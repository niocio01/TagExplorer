using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace TagExplorer.Views;

public partial class MainWindow : Window
{
    public MainWindow()
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