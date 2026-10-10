namespace SyncLib.Core.Enums;

public enum StorageConnectionType
{
    Local,
    Network,
    Ftp
}

public static class StorageConnectionTypeExtensions
{
    public static string ToDisplayName(this StorageConnectionType connectionType) => connectionType switch
    {
        StorageConnectionType.Local => "Local",
        StorageConnectionType.Network => "Rede (UNC)",
        StorageConnectionType.Ftp => "FTP",
        _ => connectionType.ToString()
    };
}
