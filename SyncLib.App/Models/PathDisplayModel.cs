using CommunityToolkit.Mvvm.ComponentModel;
using SyncLib.Core.Entities;
using SyncLib.Core.Enums;
using System;
using System.IO;

namespace SyncLib.App.Models;

public partial class PathDisplayModel : ObservableObject
{
    public Guid Id { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    private string _path = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MediaTypeDisplayName))]
    private MediaType _mediaType;

    public string MediaTypeDisplayName => MediaType.ToDisplayName();

    [ObservableProperty]
    private string _description = string.Empty;

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
        _description = entity.Description;
        _includesSubfolders = entity.IncludesSubfolders;
        CheckStatus();
    }

    public PathDisplayModel()
    {
        Id = Guid.NewGuid();
    }

    public void CheckStatus()
    {
        ExistsOnDisk = !string.IsNullOrWhiteSpace(Path) && Directory.Exists(Path);
    }
}
