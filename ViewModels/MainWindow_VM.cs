using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using TagExplorer.Data;
using TagExplorer.Views;

namespace TagExplorer.ViewModels;

internal partial class MainWindow_VM : ObservableObject
{

    private SettingsWindow_V? SettingsWindow;
    private AppDbContext _db;

    public MainWindow_VM() { } // For design time

    public MainWindow_VM(AppDbContext db)
    {
        _db = db;
    }

    [RelayCommand]
    public void OpenSettings()
    {
        SettingsWindow = new SettingsWindow_V();
        SettingsWindow.Show();
    }
}