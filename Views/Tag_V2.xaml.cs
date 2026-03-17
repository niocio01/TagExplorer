using CommunityToolkit.Mvvm.Input;
using System.Windows;
using System.Windows.Input;
using TagExplorer.Models;
using TagExplorer.Services;

namespace TagExplorer.Views
{
    /// <summary>
    /// Interaction logic for Tag_V.xaml
    /// </summary>
    public partial class Tag_V2
    {
        private Point _dragStartPoint;

        public Tag_V2()
        {
            InitializeComponent();
        }

        private void Grid_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(this);
        }

        private void Grid_PreviewMouseMove(object sender, MouseEventArgs e)
        {
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

            if (DataContext is FilterTag tag)
            {
                TagDragState.SetDragging(true);
                try
                {
                    DragDrop.DoDragDrop(this, new DataObject(typeof(FilterTag), tag), DragDropEffects.Copy);
                }
                finally
                {
                    TagDragState.SetDragging(false);
                }
            }
        }
    }
}
