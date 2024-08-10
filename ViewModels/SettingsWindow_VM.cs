using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using TagExplorer.Views;

namespace TagExplorer.ViewModels;

internal partial class SettingsWindow_VM : ObservableObject
{
    private readonly SettingsWindow_V View;
    public SettingsWindow_VM(SettingsWindow_V view)
    {
        View = view;
    }
}