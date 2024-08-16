using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using TagExplorer.Models;
using TagExplorer.Views;

namespace TagExplorer.ViewModels;

public partial class SettingsWindow_VM : ObservableObject
{
    

    [ObservableProperty]
    private DBConnectionSettings_M _DBConnectionSettings;

    [ObservableProperty]
    private DBConnector.ConnectionState _DBConnectionTestState;
    public SettingsWindow_VM()
    {
        DBConnectionSettings = new DBConnectionSettings_M();
        DBConnectionTestState = DBConnector.ConnectionState.Unknown;

    }

    [RelayCommand]
    public void CheckConnection()
    {
        if (DBConnector.CheckConnection(DBConnectionSettings))
            DBConnectionTestState = DBConnector.ConnectionState.Connected;
        else
            DBConnectionTestState = DBConnector.ConnectionState.Failed;
        DBConnectionTestState = DBConnector.ConnectionState.Connected;
    }

    [RelayCommand]
    public void SaveSettings()
    {
        Properties.Settings.Default.Host = DBConnectionSettings.Host;
        Properties.Settings.Default.Port = DBConnectionSettings.Port;
        Properties.Settings.Default.Username = DBConnectionSettings.Username;
        Properties.Settings.Default.Password = DBConnectionSettings.Password;
        Properties.Settings.Default.Save();
    }
}