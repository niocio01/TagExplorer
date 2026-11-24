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


    // Without this, Windows derives the effect from the keys held (Shift = Move), which the tag drag
    // doesn't allow, so a Shift+drop would be refused before Drop is raised.
    private void ListBoxItem_DragOver(object sender, DragEventArgs e)
    {
        var isTagPayload = e.Data.GetDataPresent(typeof(FilterTag)) || e.Data.GetDataPresent(typeof(Tag_VM));
        e.Effects = isTagPayload ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
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

        // Shift+drop on a folder: pick how the tag is assigned.
        if (payload is not null && targetItem is Folder && AssignmentModeMenu.IsShiftHeld(e) && sender is FrameworkElement dropTarget)
        {
            AssignmentModeMenu.ShowAfterDrop(dropTarget, AssignmentModeMenu.GetTagName(payload),
                mode => Vm?.TryAddDroppedTagToItem(targetItem, payload, mode));
            e.Effects = DragDropEffects.Copy;
            e.Handled = true;
            return;
        }

        var added = Vm?.TryAddDroppedTagToItem(targetItem, payload) == true;
        e.Effects = added ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

}
