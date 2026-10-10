using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using System.IO;

namespace SyncLib.App.Models;

public partial class SyncItemModel : ObservableObject
{
    [ObservableProperty]
    private string _sourceLibraryName = string.Empty;

    [ObservableProperty]
    private PathDisplayModel? _sourceConfig;

    [ObservableProperty]
    private PathDisplayModel? _destinationConfig;

    [ObservableProperty]
    private string _sourceFilePath = string.Empty;

    [ObservableProperty]
    private string _sourceFileName = string.Empty;

    [ObservableProperty]
    private string _destinationLibraryName = string.Empty;

    [ObservableProperty]
    private string _destinationFolderPath = string.Empty;

    [ObservableProperty]
    private string _destinationFilePath = string.Empty;

    [ObservableProperty]
    private string _rowColor = "Transparent";

    [ObservableProperty]
    private string _statusTooltip = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusDotColor))]
    private bool _isCopied;

    [ObservableProperty]
    private bool _hasError;

    public string StatusDotColor
    {
        get
        {
            if (HasError) return "#EF4444"; // Vermelho se erro
            if (IsCopied) return "#22C55E"; // Verde se copiado
            return "#888888"; // Cinza se pendente
        }
    }
}
