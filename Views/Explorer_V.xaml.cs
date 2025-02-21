using Microsoft.Extensions.DependencyInjection;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using TagExplorer.ViewModels;

namespace TagExplorer.Views
{
    /// <summary>
    /// Interaction logic for Tags_V.xaml
    /// </summary>
    public partial class Explorer_V : UserControl
    {
        private readonly Explorer_VM _vm;

        public Explorer_V()
        {
            InitializeComponent();

            _vm = (DataContext as Explorer_VM)!;

            MouseDown += OnMouseDownEvent;
        }

        private void OnMouseDownEvent(object sender, MouseButtonEventArgs e)
        {
            switch (e.ChangedButton)
            {
                case MouseButton.XButton1://Back button
                    _vm.GoBack();
                    e.Handled = true;
                    break;
                case MouseButton.XButton2://forward button
                    _vm.GoForward();
                    e.Handled = true;
                    break;
                default:
                    break;
            }
        }

        private void ListBoxItem_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // sender and e are not used
            // we can get the item from the selected item on the listbox
            _vm.OnItemDoubleClicked();
        }
    }
}
