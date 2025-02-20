using CommunityToolkit.Mvvm.ComponentModel;
using FuzzySharp;
using FuzzySharp.Extractor;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using TagExplorer.Data;

namespace TagExplorer.Views;

[ObservableObject]
public partial class IconSelector_V : Window
{
    private const int NumOfIconsToDisplay = 200;
    private const int InputTimerIntervalMs = 200;

    private readonly List<string> _allIconNames = Enum.GetNames(typeof(PackIconKind)).ToList();

    private List<string> _allFavourites = ["Help"];

    [ObservableProperty]
    private ObservableCollection<IconButton_V> _filteredIcons = new ObservableCollection<IconButton_V>();

    [ObservableProperty] private string? _selectedIcon;
    [ObservableProperty] private string? _filterText;
    [ObservableProperty] private bool _favouritesOnly;

    private static System.Timers.Timer _inputFinishedTimer = new(InputTimerIntervalMs);

    public IconSelector_V()
    {
        InitializeComponent();
        FilterIcons();
        _inputFinishedTimer.Elapsed += (sender, args) => Dispatcher.Invoke(FilterIcons);
        _inputFinishedTimer.AutoReset = false;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        HwndSource source = (HwndSource)PresentationSource.FromVisual(this);
        SetDarkStatusbar.UseImmersiveDarkMode(source.Handle, true);
    }

    partial void OnFilterTextChanged(string? value)
    {
        // restart the timer
        _inputFinishedTimer.Stop();
        _inputFinishedTimer.Start();
    }

    partial void OnFavouritesOnlyChanged(bool value)
    {
        FilterIcons();
    }

    private void FilterIcons()
    {
        var db = App.AppHost.Services.GetRequiredService<AppDbContext>();
        _allFavourites = db.IconFavourites.Select(i => i.IconName).ToList();

        var sorted =
            Process.ExtractTop(
                FilterText ?? "",
                FavouritesOnly ? _allFavourites : _allIconNames,
                limit: NumOfIconsToDisplay);

        var sortedFavourites = sorted.Where(n => _allFavourites.Contains(n.Value));
        var sortedNonFavourites = sorted.Where(n => !_allFavourites.Contains(n.Value));

        FilteredIcons.Clear();
        foreach (ExtractedResult<string> sortedFavourite in sortedFavourites)
        {
            FilteredIcons.Add(new IconButton_V(sortedFavourite.Value, true));
        }
        
        if (FavouritesOnly)
        {
            return;
        }
        foreach (ExtractedResult<string> sortedNonFavourite in sortedNonFavourites)
        {
            FilteredIcons.Add(new IconButton_V(sortedNonFavourite.Value));
        }
    }
}

