using SyncLib.Core.Enums;

namespace SyncLib.App.Models;

public class MediaTypeOption
{
    public MediaType Type { get; set; }
    public string DisplayName => Type.ToDisplayName();
}
