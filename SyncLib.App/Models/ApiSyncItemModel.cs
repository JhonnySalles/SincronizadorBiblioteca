using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;

namespace SyncLib.App.Models;

public partial class ApiSyncItemModel : ObservableObject
{
    [ObservableProperty]
    private bool _isSelected = true;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _extension = string.Empty;

    [ObservableProperty]
    private string _mediaType = "Mangá";

    [ObservableProperty]
    private string _status = "Pendente";

    [ObservableProperty]
    private string _statusColor = "#888888";

    [ObservableProperty]
    private string _statusDetails = string.Empty;

    private static readonly string[] s_availableMediaTypes = new[] { "Mangá", "Livro" };
    public IReadOnlyList<string> AvailableMediaTypes => s_availableMediaTypes;

    public void SetStatus(string status, string? details = null)
    {
        Status = status;
        StatusDetails = details ?? string.Empty;

        StatusColor = status switch
        {
            "Novo" => "#3B82F6",         // Azul
            "Atualizar" => "#F59E0B",    // Amarelo/Laranja
            "Sincronizado" => "#22C55E", // Verde
            "Sincronizando..." => "#A855F7", // Roxo
            "Erro" => "#EF4444",         // Vermelho
            _ => "#888888"               // Cinza
        };
    }
}
