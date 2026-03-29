using CommunityToolkit.Mvvm.Input;
using System.Windows.Controls;

namespace TagExplorer.Views;

public partial class FilterTag_V : UserControl
{
    public FilterTag_V()
    {
        InitializeComponent();
    }

    [RelayCommand]
    private void HandleLeftClick()
    {
        if (DataContext is Filter filter && filter.ToggleRequiredFilterCommand.CanExecute(null))
        {
            filter.ToggleRequiredFilterCommand.Execute(null);
        }
    }

    [RelayCommand]
    private void HandleRightClick()
    {
        if (DataContext is Filter filter && filter.ToggleDisallowedFilterCommand.CanExecute(null))
        {
            filter.ToggleDisallowedFilterCommand.Execute(null);
        }
    }
}
