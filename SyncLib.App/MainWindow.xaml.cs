using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncLib.App.Views;

namespace SyncLib_App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        AppWindow.SetIcon("Assets/AppIcon.ico");
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
