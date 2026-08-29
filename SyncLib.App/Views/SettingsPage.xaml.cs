using Microsoft.UI.Xaml.Controls;
using SyncLib.App.ViewModels;

namespace SyncLib.App.Views;

public sealed partial class SettingsPage : Page
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage()
    {
        ViewModel = new SettingsViewModel();
        DataContext = ViewModel;
        this.InitializeComponent();
    }
}
