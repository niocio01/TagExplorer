using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using TagExplorer.Models;

namespace TagExplorer.Views;

/// <summary>
/// The menu shown when a tag is dropped on a folder with Shift held, to pick how the tag is assigned.
/// </summary>
public static class AssignmentModeMenu
{
    /// <summary>
    /// Opens the menu at the mouse once the drop has finished; <paramref name="onPicked"/> runs for the picked
    /// kind. Closing the menu without a pick does nothing.
    /// </summary>
    public static void ShowAfterDrop(FrameworkElement target, string tagName, Action<AssignmentMode> onPicked)
    {
        // Opening a popup while the drag-and-drop loop still runs would close it again at once.
        target.Dispatcher.BeginInvoke(DispatcherPriority.Input, () =>
        {
            var menu = new ContextMenu
            {
                PlacementTarget = target,
                Placement = PlacementMode.MousePoint
            };

            menu.Items.Add(new MenuItem { Header = $"Assign \"{tagName}\" to", IsEnabled = false });
            menu.Items.Add(new Separator());

            foreach (var mode in AssignmentModes.All)
            {
                var item = new MenuItem { Header = AssignmentModes.DisplayName(mode) };
                item.Click += (_, _) => onPicked(mode);
                menu.Items.Add(item);
            }

            menu.IsOpen = true;
        });
    }

    public static bool IsShiftHeld(DragEventArgs e) => (e.KeyStates & DragDropKeyStates.ShiftKey) != 0;

    public static string GetTagName(object payload) => payload switch
    {
        ITag tag => tag.Name ?? string.Empty,
        ViewModels.Tag_VM tagVm => tagVm.Tag.Name ?? string.Empty,
        _ => string.Empty
    };
}
