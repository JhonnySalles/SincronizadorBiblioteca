using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using SyncLib.Core.Enums;

namespace SyncLib.App.Models;

public partial class FileItemModel : ObservableObject
{
    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _finalFileName = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _destinationFolder = string.Empty;

    [ObservableProperty]
    private string _seriesFolderName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTargetDirectory))]
    private bool _isCustomTarget;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayTargetDirectory))]
    private string _targetDirectory = string.Empty;

    [ObservableProperty]
    private string _mediaTypeDisplayName = string.Empty;

    [ObservableProperty]
    private bool _isSubfoldersActive;

    [ObservableProperty]
    private string _rowColor = "Transparent";

    [ObservableProperty]
    private string _statusTooltip = string.Empty;

    [ObservableProperty]
    private int? _volumeNumber;

    public string DisplayTargetDirectory
    {
        get
        {
            if (IsCustomTarget)
                return TargetDirectory;

            if (IsSubfoldersActive && !string.IsNullOrEmpty(SeriesFolderName))
                return SeriesFolderName;

            return TargetDirectory;
        }
        set
        {
            if (string.IsNullOrWhiteSpace(value)) return;

            if (IsCustomTarget || value.Contains('/') || value.Contains('\\'))
            {
                IsCustomTarget = true;
                TargetDirectory = value;
            }
            else if (IsSubfoldersActive)
            {
                IsCustomTarget = false;
                SeriesFolderName = value;
                TargetDirectory = Path.Combine(DestinationFolder, value);
            }
            else
            {
                IsCustomTarget = false;
                TargetDirectory = value;
                DestinationFolder = value;
            }
            OnPropertyChanged(nameof(DisplayTargetDirectory));
        }
    }
}
