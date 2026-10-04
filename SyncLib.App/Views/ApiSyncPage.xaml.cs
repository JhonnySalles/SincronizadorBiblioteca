using System;
using System.IO;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SyncLib.App.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace SyncLib.App.Views;

public sealed partial class ApiSyncPage : Page
{
    public ApiSyncViewModel ViewModel { get; } = new();

    public ApiSyncPage()
    {
        this.InitializeComponent();
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        if (ApiPasswordBox != null && !string.IsNullOrEmpty(ViewModel.ApiPassword))
        {
            ApiPasswordBox.Password = ViewModel.ApiPassword;
        }
    }

    private void ApiPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (sender is PasswordBox pb)
        {
            ViewModel.ApiPassword = pb.Password;
        }
    }

    private async void SearchPath_KeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            await ViewModel.AnalyzeFolderAsync();
        }
    }

    private async void BrowseFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.ComputerFolder;
        picker.FileTypeFilter.Add("*");

        var window = (Application.Current as SyncLib_App.App)?.MainWindow;
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            ViewModel.SearchPath = folder.Path;
            await ViewModel.AnalyzeFolderAsync();
        }
    }

    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectAll(true);
    }

    private void UnselectAll_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectAll(false);
    }

    private void Grid_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Solte a pasta ou arquivo aqui para analisar";
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.IsContentVisible = true;
    }

    private async void Grid_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var firstItem = items.FirstOrDefault();
            if (firstItem != null)
            {
                if (Directory.Exists(firstItem.Path))
                {
                    ViewModel.SearchPath = firstItem.Path;
                    await ViewModel.AnalyzeFolderAsync();
                }
                else if (File.Exists(firstItem.Path))
                {
                    var parentDir = Path.GetDirectoryName(firstItem.Path);
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        ViewModel.SearchPath = parentDir;
                        await ViewModel.AnalyzeFolderAsync();
                    }
                }
            }
        }
    }
}
