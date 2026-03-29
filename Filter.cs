using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace TagExplorer
{
    public partial class Filter : ObservableObject
    {

        public event EventHandler<FilterTypes>? FilterTypeChanged;

        partial void OnFilterTypeChanged(FilterTypes value)
        {
            FilterTypeChanged?.Invoke(this, value);
        }


        [ObservableProperty] private FilterTypes _filterType = FilterTypes.None;

        [RelayCommand]
        public void ToggleRequiredFilter()
        {
            FilterType = FilterType == FilterTypes.Required ? FilterTypes.None : FilterTypes.Required;
        }

        [RelayCommand]
        public void ToggleDisallowedFilter()
        {
            FilterType = FilterType == FilterTypes.Disallowed ? FilterTypes.None : FilterTypes.Disallowed;
        }
    }
}
