using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using TagExplorer.Views;

namespace TagExplorer.ViewModels;

internal partial class MainWindow_VM : ObservableObject
{
    SettingsWindow_V? SettingsWindow;
    public MainWindow_VM()
    {
        
    }

    [RelayCommand]
    public void OpenSettings()
    {
        SettingsWindow = new SettingsWindow_V();
        SettingsWindow.Show();
    }
}