using System;

namespace SyncLib.Core.Entities;

public class NamingPattern
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string OriginalRawSeries { get; set; } = string.Empty;
    public string CustomTemplate { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
