using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using SyncLib.App.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using System;
using System.Collections.Generic;

namespace SyncLib.App.Views;

public sealed partial class DashboardPage : Page
{
    public DashboardViewModel ViewModel { get; }

    public DashboardPage()
    {
        InitializeComponent();
        ViewModel = new DashboardViewModel();
        DataContext = ViewModel;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _ = ViewModel.LoadConfiguredPathsAsync();
    }

    private void Grid_DragOver(object sender, DragEventArgs e)
    {
        e.AcceptedOperation = DataPackageOperation.Copy;
        e.DragUIOverride.Caption = "Adicionar à fila do SyncLib";
        e.DragUIOverride.IsCaptionVisible = true;
        e.DragUIOverride.IsContentVisible = true;
    }

    private async void Grid_Drop(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var paths = new List<string>();
            foreach (var item in items)
            {
                paths.Add(item.Path);
            }

            ViewModel.AddFiles(paths);
        }
    }

    private async void OpenFilePicker_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker();
        picker.ViewMode = PickerViewMode.List;
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");

        // Necessário no WinUI 3 desktop para associar o picker à janela atual
        var window = (Application.Current as SyncLib_App.App)?.MainWindow;
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }

        var files = await picker.PickMultipleFilesAsync();
        if (files != null && files.Count > 0)
        {
            var paths = new List<string>();
            foreach (var file in files)
            {
                paths.Add(file.Path);
            }

            ViewModel.AddFiles(paths);
        }
    }

    private async void SelectCustomFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is SyncLib.App.Models.FileItemModel item)
        {
            var picker = new FolderPicker();
            picker.SuggestedStartLocation = PickerLocationId.Desktop;
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
                item.IsCustomTarget = true;
                item.TargetDirectory = folder.Path;
                item.RowColor = "Transparent";
                item.StatusTooltip = $"Pasta personalizada selecionada: {folder.Path}";

                ViewModel.UpdateDirectoryCache(item.SeriesFolderName, folder.Path);
            }
        }
    }

    private void RemoveFile_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is SyncLib.App.Models.FileItemModel item)
        {
            ViewModel.RemovePendingFileCommand.Execute(item);
        }
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.DataContext is SyncLib.App.Models.FileItemModel item)
        {
            ViewModel.OpenCopiedFolderCommand.Execute(item);
        }
    }
}
