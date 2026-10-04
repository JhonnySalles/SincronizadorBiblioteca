using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SyncLib.App.Models;
using SyncLib.Core.Services;

namespace SyncLib.App.ViewModels;

public partial class ApiSyncViewModel : ObservableObject
{
    private readonly ApiSyncService _apiSyncService = new();

    [ObservableProperty]
    private string _searchPath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private double _processProgress;

    [ObservableProperty]
    private double _processTotal;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private bool _isProcessing;

    [ObservableProperty]
    private bool _isApiOnline;

    [ObservableProperty]
    private string _apiStatusTooltip = "Verificando API...";

    [ObservableProperty]
    private string _apiStatusColor = "#888888";

    public ObservableCollection<ApiSyncItemModel> Items { get; } = new();

    public ApiSyncViewModel()
    {
        _ = CheckApiStatusAsync();
    }

    [RelayCommand]
    public async Task CheckApiStatusAsync()
    {
        ApiStatusTooltip = "Verificando API...";
        IsApiOnline = await _apiSyncService.IsApiOnlineAsync();

        if (IsApiOnline)
        {
            ApiStatusTooltip = "API Online";
            ApiStatusColor = "#22C55E";
        }
        else
        {
            ApiStatusTooltip = "API Offline";
            ApiStatusColor = "#EF4444";
        }
    }

    [RelayCommand]
    public async Task AnalyzeFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchPath) || !Directory.Exists(SearchPath))
        {
            StatusMessage = "Selecione ou digite um diretório válido.";
            return;
        }

        IsBusy = true;
        IsAnalyzing = true;
        StatusMessage = "Buscando arquivos na pasta...";
        Items.Clear();

        try
        {
            var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                ".cbz", ".cbr", ".zip", ".epub"
            };

            var files = await Task.Run(() =>
            {
                try
                {
                    return Directory.EnumerateFiles(SearchPath, "*.*", SearchOption.AllDirectories)
                        .Where(f => allowedExtensions.Contains(Path.GetExtension(f)))
                        .ToList();
                }
                catch
                {
                    return Directory.EnumerateFiles(SearchPath, "*.*", SearchOption.TopDirectoryOnly)
                        .Where(f => allowedExtensions.Contains(Path.GetExtension(f)))
                        .ToList();
                }
            });

            if (files.Count == 0)
            {
                StatusMessage = "Nenhum arquivo de Mangá ou Livro (.cbz, .epub, .zip, .cbr) encontrado.";
                IsBusy = false;
                IsAnalyzing = false;
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

            StatusMessage = $"Encontrados {Items.Count} arquivos. Verificando situação na API...";
            await CheckApiStatusAsync();

            if (!IsApiOnline)
            {
                StatusMessage = $"{Items.Count} arquivos carregados. Atenção: API está offline.";
                foreach (var it in Items)
                {
                    it.SetStatus("Offline", "API não acessível.");
                }
                IsBusy = false;
                IsAnalyzing = false;
                return;
            }

            // Checagem na API
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
