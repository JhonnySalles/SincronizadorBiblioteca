using CommunityToolkit.WinUI.UI.Controls;
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

    private void ConfiguredPathsDataGrid_Sorting(object sender, DataGridColumnEventArgs e)
    {
        var currentSort = e.Column.SortDirection;
        var newSort = (currentSort == DataGridSortDirection.Ascending)
            ? DataGridSortDirection.Descending
            : DataGridSortDirection.Ascending;

        foreach (var col in ConfiguredPathsDataGrid.Columns)
        {
            if (col != e.Column)
            {
                col.SortDirection = null;
            }
        }

        e.Column.SortDirection = newSort;

        var tag = e.Column.Tag?.ToString() ?? e.Column.Header?.ToString();
        ViewModel.SortPaths(tag, newSort == DataGridSortDirection.Ascending);
    }
}
