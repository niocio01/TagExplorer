using System.Windows.Controls;
using System.Windows;
using TagExplorer.Models;
using TagExplorer.Services;
using TagExplorer.ViewModels;

namespace TagExplorer.Views;

public partial class ItemDetails_V : UserControl
{
    public ItemDetails_V()
    {
        InitializeComponent();
        Loaded += ItemDetails_V_Loaded;
        Unloaded += ItemDetails_V_Unloaded;
    }

    private void ItemDetails_V_Loaded(object sender, RoutedEventArgs e)
    {
        TagDragState.IsDraggingChanged += TagDragState_IsDraggingChanged;
        if (TagDragState.IsDragging)
        {
            DropHintOverlay.Visibility = Visibility.Visible;
        }
    }

    private void ItemDetails_V_Unloaded(object sender, RoutedEventArgs e)
    {
        TagDragState.IsDraggingChanged -= TagDragState_IsDraggingChanged;
    }

    private void TagDragState_IsDraggingChanged(object? sender, bool isDragging)
    {
        Dispatcher.Invoke(() =>
        {
            if (isDragging)
            {
                DropHintOverlay.Visibility = Visibility.Visible;
                return;
            }

            RestoreDropHintVisibility();
        });
    }

    private void TagsExpander_DragOver(object sender, DragEventArgs e)
    {
        if (IsTagPayload(e))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void TagsExpander_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not ItemDetails_VM vm)
        {
            return;
        }

        object? payload = e.Data.GetData(typeof(FilterTag)) ?? e.Data.GetData(typeof(Tag_VM));
        if (vm.TryAddDroppedTag(payload))
        {
            e.Effects = DragDropEffects.Copy;
        }

        RestoreDropHintVisibility();

        e.Handled = true;
    }

    private static bool IsTagPayload(DragEventArgs e)
    {
        return e.Data.GetDataPresent(typeof(FilterTag)) || e.Data.GetDataPresent(typeof(Tag_VM));
    }

    private void RestoreDropHintVisibility()
    {
        DropHintOverlay.ClearValue(VisibilityProperty);
    }
}
