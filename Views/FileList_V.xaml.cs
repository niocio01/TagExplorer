using System.Windows.Controls;
using System.Windows.Input;
using TagExplorer.ViewModels;

namespace TagExplorer.Views;

public partial class FileList_V : UserControl
{
    private FileList_VM? Vm => DataContext as FileList_VM;

    public FileList_V()
    {
        InitializeComponent();
    }

    private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        Vm?.HandleItemDoubleClick();
    }
}
