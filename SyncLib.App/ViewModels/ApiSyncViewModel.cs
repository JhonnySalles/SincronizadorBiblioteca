using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml;
using SyncLib.App.Models;
using SyncLib.Core.Services;

namespace SyncLib.App.ViewModels;

public partial class ApiSyncViewModel : ObservableObject
{
    private readonly ApiSyncService _apiSyncService = new();

    [ObservableProperty]
    private string _searchPath = string.Empty;

    [ObservableProperty]
    private string _apiUsername = string.Empty;

    [ObservableProperty]
    private string _apiPassword = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private double _processProgress;

    [ObservableProperty]
    private double _processTotal;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isNotBusy = true;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isApiOnline;

    [ObservableProperty]
    private bool _isApiAuthenticated;

    [ObservableProperty]
    private string _apiStatusButtonText = "Status da API";

    [ObservableProperty]
    private string _apiStatusTooltip = "Verificando API...";

    [ObservableProperty]
    private string _apiStatusColor = "#888888";

    [ObservableProperty]
    private Visibility _placeholderVisibility = Visibility.Visible;

    public ObservableCollection<ApiSyncItemModel> Items { get; } = new();

    public ApiSyncViewModel()
    {
        ApiUsername = _apiSyncService.Username;
        ApiPassword = _apiSyncService.Password;
        _ = CheckApiStatusAsync();
    }

    partial void OnIsBusyChanged(bool value)
    {
        IsNotBusy = !value;
    }

    partial void OnApiUsernameChanged(string value)
    {
        _apiSyncService.SaveSettings(value, ApiPassword);
        _ = CheckApiStatusAsync();
    }

    partial void OnApiPasswordChanged(string value)
    {
        _apiSyncService.SaveSettings(ApiUsername, value);
        _ = CheckApiStatusAsync();
    }

    [RelayCommand]
    public async Task CheckApiStatusAsync()
    {
        ApiStatusTooltip = "Verificando conexão com a API...";
        ApiStatusButtonText = "Verificando...";
        ApiStatusColor = "#888888";
        StatusMessage = $"Verificando conexão com a API em {_apiSyncService.BaseUrl}...";

        IsApiOnline = await _apiSyncService.IsApiOnlineAsync(3);

        if (IsApiOnline)
        {
            IsApiAuthenticated = false;
            if (!string.IsNullOrWhiteSpace(ApiUsername))
            {
                IsApiAuthenticated = await _apiSyncService.AuthenticateAsync(ApiUsername, ApiPassword);
            }

            if (IsApiAuthenticated)
            {
                ApiStatusButtonText = "API Online";
                ApiStatusTooltip = $"API Online e Autenticada ({_apiSyncService.BaseUrl})";
                ApiStatusColor = "#22C55E"; // Verde
                StatusMessage = $"API Online e autenticada com sucesso ({_apiSyncService.BaseUrl})!";
            }
            else
            {
                ApiStatusButtonText = "Não Autenticada";
                var errorMsg = !string.IsNullOrWhiteSpace(_apiSyncService.LastAuthError) 
                    ? _apiSyncService.LastAuthError 
                    : "Informe usuário e senha válidos.";
                ApiStatusTooltip = $"API Online em {_apiSyncService.BaseUrl}, mas a autenticação falhou: {errorMsg}";
                ApiStatusColor = "#F59E0B"; // Laranja
                StatusMessage = $"API Online em {_apiSyncService.BaseUrl}, mas a autenticação falhou: {errorMsg}";
            }
        }
        else
        {
            IsApiAuthenticated = false;
            ApiStatusButtonText = "API Offline";
            ApiStatusTooltip = $"API Offline - Não respondeu em {_apiSyncService.BaseUrl}";
            ApiStatusColor = "#EF4444"; // Vermelho
            StatusMessage = $"API Offline! Não foi possível conectar ao servidor em {_apiSyncService.BaseUrl}.";
        }
    }

    [RelayCommand]
    public async Task AnalyzeFolderAsync()
    {
        var cleanPath = SearchPath?.Trim(' ', '"', '\'') ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cleanPath) || !Directory.Exists(cleanPath))
        {
            StatusMessage = "Selecione ou digite um diretório válido existente.";
            return;
        }

        IsBusy = true;
        IsAnalyzing = true;
        StatusMessage = "Buscando arquivos na pasta...";
        Items.Clear();
        PlaceholderVisibility = Visibility.Visible;

        try
        {
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".cbz", ".cbr", ".zip", ".epub"
            };

            var files = await Task.Run(() =>
            {
                var found = new List<string>();
                var queue = new Queue<string>();
                queue.Enqueue(cleanPath);

                while (queue.Count > 0)
                {
                    var currentDir = queue.Dequeue();
                    try
                    {
                        foreach (var subDir in Directory.EnumerateDirectories(currentDir))
                        {
                            queue.Enqueue(subDir);
                        }
                    }
                    catch { }

                    try
                    {
                        foreach (var f in Directory.EnumerateFiles(currentDir, "*.*", SearchOption.TopDirectoryOnly))
                        {
                            if (allowedExtensions.Contains(Path.GetExtension(f)))
                            {
                                found.Add(f);
                            }
                        }
                    }
                    catch { }
                }

                return found;
            });

            if (files.Count == 0)
            {
                StatusMessage = "Nenhum arquivo de Mangá ou Livro (.cbz, .epub, .zip, .cbr) encontrado no diretório.";
                PlaceholderVisibility = Visibility.Visible;
                return;
            }

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file);
                var isBook = string.Equals(ext, ".epub", StringComparison.OrdinalIgnoreCase);

                var item = new ApiSyncItemModel
                {
                    FilePath = file,
                    FileName = Path.GetFileName(file),
                    Extension = ext,
                    MediaType = isBook ? "Livro" : "Mangá",
                    IsSelected = true
                };
                item.SetStatus("Pendente");
                Items.Add(item);
            }

            PlaceholderVisibility = Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

            StatusMessage = $"Encontrados {Items.Count} arquivos. Verificando situação na API...";
            await CheckApiStatusAsync();

            if (!IsApiOnline)
            {
                StatusMessage = $"{Items.Count} arquivo(s) carregados da pasta. Atenção: A API está Offline.";
                foreach (var it in Items)
                {
                    it.SetStatus("Offline", "API não acessível.");
                }
                return;
            }

            if (!IsApiAuthenticated)
            {
                StatusMessage = $"{Items.Count} arquivo(s) carregados da pasta. Atenção: Autenticação pendente na API. Informe usuário e senha válidos.";
                foreach (var it in Items)
                {
                    it.SetStatus("Não Autenticado", "Faça login com usuário e senha para consultar a API.");
                }
                return;
            }

            // Checagem na API quando online E autenticada
            int checkedCount = 0;
            ProcessTotal = Items.Count;
            ProcessProgress = 0;

            foreach (var item in Items)
            {
                item.SetStatus("Analisando...");
                var status = await _apiSyncService.CheckFileStatusAsync(item.FilePath, item.FileName, item.MediaType);
                item.SetStatus(status);

                checkedCount++;
                ProcessProgress = checkedCount;
                StatusMessage = $"Verificando API ({checkedCount}/{Items.Count}): {item.FileName}";
            }

            int novos = Items.Count(i => i.Status == "Novo");
            int atualizar = Items.Count(i => i.Status == "Atualizar");
            StatusMessage = $"Análise concluída: {novos} Novos, {atualizar} para Atualizar.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao analisar pasta: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsAnalyzing = false;
        }
    }

    [RelayCommand]
    public async Task ProcessAsync()
    {
        var selectedItems = Items.Where(i => i.IsSelected).ToList();
        if (selectedItems.Count == 0)
        {
            StatusMessage = "Nenhum arquivo selecionado para processar.";
            return;
        }

        await CheckApiStatusAsync();
        if (!IsApiOnline)
        {
            StatusMessage = "Não é possível processar: a API está Offline.";
            return;
        }

        if (!IsApiAuthenticated)
        {
            StatusMessage = "Não é possível processar: Autenticação obrigatória. Verifique o usuário e senha da API.";
            return;
        }

        IsBusy = true;
        IsProcessing = true;
        ProcessTotal = selectedItems.Count;
        ProcessProgress = 0;
        int successCount = 0;
        int errorCount = 0;

        foreach (var item in selectedItems)
        {
            item.SetStatus("Sincronizando...");
            StatusMessage = $"Processando ({successCount + errorCount + 1}/{selectedItems.Count}): {item.FileName}...";

            try
            {
                if (string.Equals(item.MediaType, "Livro", StringComparison.OrdinalIgnoreCase))
                {
                    await _apiSyncService.SyncEpubAsync(item.FilePath, item.FileName, "pt");
                }
                else
                {
                    await _apiSyncService.SyncComicAsync(item.FilePath, item.FileName, "pt");
                }

                item.SetStatus("Sincronizado");
                successCount++;
            }
            catch (Exception ex)
            {
                item.SetStatus("Erro", ex.Message);
                errorCount++;
            }

            ProcessProgress = successCount + errorCount;
        }

        IsBusy = false;
        IsProcessing = false;
        StatusMessage = $"Processamento concluído! {successCount} sucesso, {errorCount} falhas.";
    }

    [RelayCommand]
    public void SelectAll(bool select)
    {
        foreach (var item in Items)
        {
            item.IsSelected = select;
        }
    }
}
