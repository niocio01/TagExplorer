using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using TagExplorer.Data;

namespace TagExplorer.Views;
[ObservableObject]
public partial class IconButton_V : UserControl
{
    public event EventHandler IconSelected;

    [ObservableProperty]
    private string _iconName = "";

    [ObservableProperty]
    private bool _isFavourite;

    public IconButton_V(string iconName, bool isFavourite = false)
    {
        IconName = iconName;
        IsFavourite = isFavourite;
        InitializeComponent();
    }

    public IconButton_V() {}

    [RelayCommand]
    public void ButtonClicked()
    {
        WeakReferenceMessenger.Default.Send(new TagIconSelected(IconName));
    }

    [RelayCommand]
    public void ButtonShiftClicked()
    {
        IsFavourite = !IsFavourite;

        var db = App.AppHost.Services.GetRequiredService<AppDbContext>();

        if (IsFavourite)
        {
           db.IconFavourites.Add(new IconFavourite(IconName));
           db.SaveChanges();
        }
        else
        {
            IconFavourite? icon = db.IconFavourites.FirstOrDefault(i => i.IconName == IconName);
            if (icon == null) return;

            db.IconFavourites.Remove(icon);
            db.SaveChanges();
        }
    }


}

public class TagIconSelected(String iconName) : ValueChangedMessage<String>(iconName);
