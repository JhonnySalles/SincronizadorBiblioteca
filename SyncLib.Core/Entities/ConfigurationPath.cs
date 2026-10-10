using System;
using SyncLib.Core.Enums;

namespace SyncLib.Core.Entities;

public class ConfigurationPath
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Path { get; set; } = string.Empty;
    public MediaType MediaType { get; set; }
    public bool IncludesSubfolders { get; set; }
    public string Description { get; set; } = string.Empty;
    public string CustomSuffix { get; set; } = string.Empty;
    public string CustomFolderSuffix { get; set; } = string.Empty;
    public string AllowedExtensions { get; set; } = string.Empty;
    public StorageConnectionType ConnectionType { get; set; } = StorageConnectionType.Local;
    public string ServerHost { get; set; } = string.Empty;
    public int ServerPort { get; set; } = 0;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}
