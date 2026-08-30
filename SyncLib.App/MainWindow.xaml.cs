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

    private static string PaneStateFilePath => System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData),
        "SyncLib",
        "pane_state.txt"
    );

    private void RestorePaneState()
    {
        try
        {
            if (System.IO.File.Exists(PaneStateFilePath))
            {
                var content = System.IO.File.ReadAllText(PaneStateFilePath);
                if (bool.TryParse(content, out var isOpen))
                {
                    NavView.IsPaneOpen = isOpen;
                }
            }
        }
        catch { }
    }

    private void SavePaneState(bool isOpen)
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(PaneStateFilePath);
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            System.IO.File.WriteAllText(PaneStateFilePath, isOpen.ToString());
        }
        catch { }
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
