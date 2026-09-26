using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SyncLib.App.Models;
using SyncLib.App.ViewModels;
using System;

namespace SyncLib.App.Views;

public sealed partial class SyncPage : Page
{
    public SyncViewModel ViewModel { get; }

    public SyncPage()
    {
        InitializeComponent();
        ViewModel = new SyncViewModel();
        DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = ViewModel.LoadConfiguredPathsAsync();
    }

    private void RemoveItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is SyncItemModel item)
        {
            ViewModel.RemoveSyncItemCommand.Execute(item);
        }
    }

    private void ConfiguredPath_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PathDisplayModel item)
        {
            if (System.IO.Directory.Exists(item.Path))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = item.Path,
                        UseShellExecute = true,
                        Verb = "open"
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Erro ao abrir pasta: {ex.Message}");
                }
            }
        }
    }
}
