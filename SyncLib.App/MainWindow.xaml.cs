using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncLib.App.Views;
using Windows.Storage;

namespace SyncLib_App;

public sealed partial class MainWindow : Window
{
    private const string IsNavPaneOpenKey = "IsNavPaneOpen";

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/WindowIcon.png");

        RestorePaneState();
    }

    private void RestorePaneState()
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        if (localSettings.Values.TryGetValue(IsNavPaneOpenKey, out var isOpenObj) && isOpenObj is bool isOpen)
        {
            NavView.IsPaneOpen = isOpen;
        }
    }

    private void SavePaneState(bool isOpen)
    {
        var localSettings = ApplicationData.Current.LocalSettings;
        localSettings.Values[IsNavPaneOpenKey] = isOpen;
    }

    private void NavView_PaneOpened(NavigationView sender, object args)
    {
        SavePaneState(true);
    }

    private void NavView_PaneClosed(NavigationView sender, object args)
    {
        SavePaneState(false);
    }

    private void NavView_Loaded(object sender, RoutedEventArgs e)
    {
        // Navegação inicial para a aba Geral
        NavView.SelectedItem = NavView.MenuItems[0];
        RootFrame.Navigate(typeof(DashboardPage));
    }

    private void NavView_ItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
        {
            RootFrame.Navigate(typeof(SettingsPage));
        }
        else if (args.InvokedItemContainer != null)
        {
            var tag = args.InvokedItemContainer.Tag?.ToString();
            if (tag == "DashboardPage")
            {
                RootFrame.Navigate(typeof(DashboardPage));
            }
        }
    }
}
