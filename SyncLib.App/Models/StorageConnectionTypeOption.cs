using SyncLib.Core.Enums;

namespace SyncLib.App.Models;

public class StorageConnectionTypeOption
{
    public StorageConnectionType Type { get; set; }
    public string DisplayName => Type.ToDisplayName();
}
