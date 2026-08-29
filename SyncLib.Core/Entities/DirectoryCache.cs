using System;
using SyncLib.Core.Enums;

namespace SyncLib.Core.Entities;

public class DirectoryCache
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RootPath { get; set; } = string.Empty;
    public string SeriesName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public MediaType MediaType { get; set; }
    public DateTime LastScanned { get; set; }
}
