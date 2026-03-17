using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TagExplorer.Models;
using TagExplorer.Services;
using TagExplorer.ViewModels;

namespace TagExplorer.Views;

public partial class TagListDroppable_V : UserControl
{
    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(
            nameof(SelectedItem),
            typeof(object),
            typeof(TagListDroppable_V),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(
            nameof(ItemsSource),
            typeof(IEnumerable),
            typeof(TagListDroppable_V),
            new PropertyMetadata(null));

    public static readonly DependencyProperty TagDroppedCommandProperty =
        DependencyProperty.Register(
            nameof(TagDroppedCommand),
            typeof(ICommand),
            typeof(TagListDroppable_V),
            new PropertyMetadata(null));

    public static readonly DependencyProperty HeaderTextProperty =
        DependencyProperty.Register(
            nameof(HeaderText),
            typeof(string),
            typeof(TagListDroppable_V),
            new PropertyMetadata("Tags"));

    public static readonly DependencyProperty NoSelectionTextProperty =
        DependencyProperty.Register(
            nameof(NoSelectionText),
            typeof(string),
            typeof(TagListDroppable_V),
            new PropertyMetadata("No item selected"));

    public static readonly DependencyProperty IsExpandedProperty =
        DependencyProperty.Register(
            nameof(IsExpanded),
            typeof(bool),
            typeof(TagListDroppable_V),
            new PropertyMetadata(true));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public ICommand? TagDroppedCommand
    {
        get => (ICommand?)GetValue(TagDroppedCommandProperty);
        set => SetValue(TagDroppedCommandProperty, value);
    }

    public string HeaderText
    {
        get => (string)GetValue(HeaderTextProperty);
        set => SetValue(HeaderTextProperty, value);
    }

    public string NoSelectionText
    {
        get => (string)GetValue(NoSelectionTextProperty);
        set => SetValue(NoSelectionTextProperty, value);
    }

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    public TagListDroppable_V()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        TagDragState.IsDraggingChanged += TagDragState_IsDraggingChanged;
        if (TagDragState.IsDragging)
        {
            DropHintOverlay.Visibility = Visibility.Visible;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
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

    private void TagsDropZone_DragOver(object sender, DragEventArgs e)
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

    private void TagsDropZone_Drop(object sender, DragEventArgs e)
    {
        var payload = e.Data.GetData(typeof(FilterTag)) ?? e.Data.GetData(typeof(Tag_VM));

        if (payload is not null && TagDroppedCommand?.CanExecute(payload) == true)
        {
            TagDroppedCommand.Execute(payload);
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
