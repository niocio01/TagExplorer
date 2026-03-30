using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TagExplorer.Models;
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

    private void Item_DragEnter(object sender, DragEventArgs e)
    {
        if (sender is ListBoxItem item)
        {
            item.Tag = "IsDraggingOver";
        }
    }

    private void Item_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is ListBoxItem item)
        {
            item.Tag = null;
        }
    }


    private void ListBoxItem_Drop(object sender, DragEventArgs e)
    {
        if (sender is ListBoxItem item)
        {
            item.Tag = null;
        }

        if (sender is not FrameworkElement { DataContext: ExplorerItem targetItem })
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var payload = e.Data.GetData(typeof(FilterTag)) ?? e.Data.GetData(typeof(Tag_VM));
        var added = Vm?.TryAddDroppedTagToItem(targetItem, payload) == true;
        e.Effects = added ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

}
