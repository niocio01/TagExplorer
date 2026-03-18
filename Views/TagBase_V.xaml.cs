using CommunityToolkit.Mvvm.Input;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TagExplorer.Models;
using TagExplorer.Services;
using TagExplorer.ViewModels;

namespace TagExplorer.Views;

public partial class TagBase_V : UserControl
{
    public static readonly DependencyProperty ClickCommandProperty =
        DependencyProperty.Register(
            nameof(ClickCommand),
            typeof(ICommand),
            typeof(TagBase_V),
            new PropertyMetadata(null));

    public static readonly DependencyProperty ClickCommandParameterProperty =
        DependencyProperty.Register(
            nameof(ClickCommandParameter),
            typeof(object),
            typeof(TagBase_V),
            new PropertyMetadata(null));

    public static readonly DependencyProperty RightClickCommandProperty =
        DependencyProperty.Register(
            nameof(RightClickCommand),
            typeof(ICommand),
            typeof(TagBase_V),
            new PropertyMetadata(null));

    public static readonly DependencyProperty RightClickCommandParameterProperty =
        DependencyProperty.Register(
            nameof(RightClickCommandParameter),
            typeof(object),
            typeof(TagBase_V),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsPickUpableProperty =
        DependencyProperty.Register(
            nameof(IsPickUpable),
            typeof(bool),
            typeof(TagBase_V),
            new PropertyMetadata(true));

    public static readonly DependencyProperty ChipStyleProperty =
        DependencyProperty.Register(
            nameof(ChipStyle),
            typeof(Style),
            typeof(TagBase_V),
            new PropertyMetadata(null));

    private Point _dragStartPoint;
    private bool _wasDragged;

    public ICommand? ClickCommand
    {
        get => (ICommand?)GetValue(ClickCommandProperty);
        set => SetValue(ClickCommandProperty, value);
    }

    public object? ClickCommandParameter
    {
        get => GetValue(ClickCommandParameterProperty);
        set => SetValue(ClickCommandParameterProperty, value);
    }

    public ICommand? RightClickCommand
    {
        get => (ICommand?)GetValue(RightClickCommandProperty);
        set => SetValue(RightClickCommandProperty, value);
    }

    public object? RightClickCommandParameter
    {
        get => GetValue(RightClickCommandParameterProperty);
        set => SetValue(RightClickCommandParameterProperty, value);
    }

    public bool IsPickUpable
    {
        get => (bool)GetValue(IsPickUpableProperty);
        set => SetValue(IsPickUpableProperty, value);
    }

    public Style? ChipStyle
    {
        get => (Style?)GetValue(ChipStyleProperty);
        set => SetValue(ChipStyleProperty, value);
    }

    public TagBase_V()
    {
        InitializeComponent();
    }

    private void Grid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(this);
        _wasDragged = false;
    }

    private void Grid_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!IsPickUpable)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var currentPosition = e.GetPosition(this);
        var delta = currentPosition - _dragStartPoint;

        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        if (DataContext is FilterTag filterTag)
        {
            _wasDragged = true;
            TagDragState.SetDragging(true);
            try
            {
                DragDrop.DoDragDrop(this, new DataObject(typeof(FilterTag), filterTag), DragDropEffects.Copy);
            }
            finally
            {
                TagDragState.SetDragging(false);
            }
            return;
        }

        if (DataContext is Tag_VM tagVm)
        {
            _wasDragged = true;
            TagDragState.SetDragging(true);
            try
            {
                DragDrop.DoDragDrop(this, new DataObject(typeof(Tag_VM), tagVm), DragDropEffects.Copy);
            }
            finally
            {
                TagDragState.SetDragging(false);
            }
        }
    }

    [RelayCommand]
    private void HandleLeftClick()
    {
        if (_wasDragged)
        {
            _wasDragged = false;
            return;
        }

        var parameter = ClickCommandParameter ?? DataContext;
        if (ClickCommand?.CanExecute(parameter) == true)
        {
            ClickCommand.Execute(parameter);
        }
    }

    [RelayCommand]
    private void HandleRightClick()
    {
        if (_wasDragged)
        {
            _wasDragged = false;
            return;
        }

        var parameter = RightClickCommandParameter ?? ClickCommandParameter ?? DataContext;
        if (RightClickCommand?.CanExecute(parameter) == true)
        {
            RightClickCommand.Execute(parameter);
        }
    }
}
