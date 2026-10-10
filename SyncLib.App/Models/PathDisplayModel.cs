using CommunityToolkit.Mvvm.ComponentModel;
using SyncLib.Core.Entities;
using SyncLib.Core.Enums;
using SyncLib.Infrastructure.Services.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace SyncLib.App.Models;

public partial class PathDisplayModel : ObservableObject
{
    public Guid Id { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    private string _path = string.Empty;

    partial void OnPathChanged(string value)
    {
        CheckStatus();
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MediaTypeDisplayName))]
    private MediaType _mediaType;

    public string MediaTypeDisplayName => MediaType.ToDisplayName();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionTypeDisplayName))]
    [NotifyPropertyChangedFor(nameof(IsFtp))]
    [NotifyPropertyChangedFor(nameof(IsNetworkOrFtp))]
    private StorageConnectionType _connectionType = StorageConnectionType.Local;

    public string ConnectionTypeDisplayName => ConnectionType.ToDisplayName();

    public bool IsFtp => ConnectionType == StorageConnectionType.Ftp;
    public bool IsNetworkOrFtp => ConnectionType != StorageConnectionType.Local;

    [ObservableProperty]
    private string _serverHost = string.Empty;

    [ObservableProperty]
    private int _serverPort = 21;

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _customSuffix = string.Empty;

    [ObservableProperty]
    private string _customFolderSuffix = string.Empty;

    [ObservableProperty]
    private string _allowedExtensions = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SubfoldersText))]
    private bool _includesSubfolders;

    public string SubfoldersText => IncludesSubfolders ? "Sim" : "Não";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    private bool _existsOnDisk;

    public string StatusText => ExistsOnDisk ? "Ativo" : "Inativo";

    public string StatusColor => ExistsOnDisk ? "#22C55E" : "#EF4444";

    public PathDisplayModel(ConfigurationPath entity)
    {
        Id = entity.Id;
        _path = entity.Path;
        _mediaType = entity.MediaType;
        _connectionType = entity.ConnectionType;
        _serverHost = entity.ServerHost;
        _serverPort = entity.ServerPort > 0 ? entity.ServerPort : 21;
        _username = entity.Username;
        _password = entity.Password;
        _description = entity.Description;
        _customSuffix = entity.CustomSuffix;
        _customFolderSuffix = entity.CustomFolderSuffix;
        _allowedExtensions = entity.AllowedExtensions;
        _includesSubfolders = entity.IncludesSubfolders;
        CheckStatus();
    }

    public PathDisplayModel()
    {
        Id = Guid.NewGuid();
    }

    public void CheckStatus()
    {
        if (ConnectionType == StorageConnectionType.Local || ConnectionType == StorageConnectionType.Network)
        {
            ExistsOnDisk = !string.IsNullOrWhiteSpace(Path) && Directory.Exists(Path);
        }
        else if (ConnectionType == StorageConnectionType.Ftp)
        {
            ExistsOnDisk = !string.IsNullOrWhiteSpace(ServerHost);
        }
    }

    public async Task CheckStatusAsync()
    {
        if (ConnectionType == StorageConnectionType.Local || ConnectionType == StorageConnectionType.Network)
        {
            ExistsOnDisk = !string.IsNullOrWhiteSpace(Path) && Directory.Exists(Path);
        }
        else if (ConnectionType == StorageConnectionType.Ftp)
        {
            if (string.IsNullOrWhiteSpace(ServerHost))
            {
                ExistsOnDisk = false;
                return;
            }

            try
            {
                using var ftp = new FtpStorageProvider(ServerHost, ServerPort, Username, Password);
                var connected = await ftp.TestConnectionAsync();
                if (connected && !string.IsNullOrWhiteSpace(Path))
                {
                    ExistsOnDisk = await ftp.DirectoryExistsAsync(Path);
                }
                else
                {
                    ExistsOnDisk = connected;
                }
            }
            catch
            {
                ExistsOnDisk = false;
            }
        }
    }
}
