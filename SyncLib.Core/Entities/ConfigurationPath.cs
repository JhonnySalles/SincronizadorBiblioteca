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
}
