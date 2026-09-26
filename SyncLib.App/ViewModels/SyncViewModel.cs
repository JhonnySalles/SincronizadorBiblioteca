using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SyncLib.App.Models;
using SyncLib.Core.Entities;
using SyncLib.Core.Enums;
using SyncLib.Core.Helpers;
using SyncLib.Infrastructure.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SyncLib.App.ViewModels;

public partial class SyncViewModel : ObservableObject
{
    public ObservableCollection<PathDisplayModel> ConfiguredPaths { get; } = new();

    public List<MediaTypeOption> MediaTypeOptions { get; } = Enum.GetValues<MediaType>()
        .Select(t => new MediaTypeOption { Type = t })
        .ToList();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DestinationPathsText))]
    private MediaTypeOption? _selectedMediaTypeOption;

    public string DestinationPathsText
    {
        get
        {
            if (SelectedMediaTypeOption == null)
                return "Nenhum tipo selecionado";

            var paths = ConfiguredPaths
                .Where(p => p.MediaType == SelectedMediaTypeOption.Type)
                .Select(p => p.Path)
                .ToList();

            if (!paths.Any())
                return "Nenhum caminho configurado para este tipo";

            return string.Join(", ", paths);
        }
    }

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private double _progressTotal;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private bool _isProcessing;

    public ObservableCollection<SyncItemModel> SyncItems { get; } = new();

    public SyncViewModel()
    {
        SelectedMediaTypeOption = MediaTypeOptions.FirstOrDefault();
        _ = LoadConfiguredPathsAsync();
    }

    [RelayCommand]
    public async Task LoadConfiguredPathsAsync()
    {
        try
        {
            using var db = new AppDbContext();
            await db.Database.MigrateAsync();

            var entities = await db.ConfigurationPaths.ToListAsync();
            ConfiguredPaths.Clear();

            var sorted = entities
                .Select(e => new PathDisplayModel(e))
                .OrderBy(p => p.MediaTypeDisplayName)
                .ThenBy(p => p.Description)
                .ToList();

            foreach (var model in sorted)
            {
                ConfiguredPaths.Add(model);
            }

            OnPropertyChanged(nameof(DestinationPathsText));
            StatusMessage = "Caminhos carregados.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao carregar caminhos: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task AnalyzeLibrariesAsync()
    {
        if (SelectedMediaTypeOption == null)
        {
            StatusMessage = "Selecione um Tipo de Processo primeiro.";
            return;
        }

        var targetType = SelectedMediaTypeOption.Type;
        var relevantPaths = ConfiguredPaths
            .Where(p => p.MediaType == targetType && p.ExistsOnDisk && Directory.Exists(p.Path))
            .ToList();

        if (relevantPaths.Count < 2)
        {
            StatusMessage = "É necessário ter pelo menos 2 pastas configuradas e existentes no disco para comparar.";
            return;
        }

        IsBusy = true;
        IsAnalyzing = true;
        StatusMessage = "Analisando pastas em paralelo...";
        SyncItems.Clear();

        try
        {
            // Estrutura para armazenar arquivos indexados por biblioteca
            // Key: PathDisplayModel, Value: Dicionário de (RelativePath Normalizado -> (RelativePath Real, FullPath))
            var libraryIndexes = new ConcurrentDictionary<PathDisplayModel, Dictionary<string, (string RelativePath, string FullPath)>>();

            await Task.Run(() =>
            {
                Parallel.ForEach(relevantPaths, lib =>
                {
                    var fileDict = new Dictionary<string, (string RelativePath, string FullPath)>(StringComparer.OrdinalIgnoreCase);

                    try
                    {
                        var searchOption = lib.IncludesSubfolders ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                        var allFiles = Directory.EnumerateFiles(lib.Path, "*.*", searchOption);

                        // Parse e normalização das extensões permitidas
                        HashSet<string>? allowedExtsSet = null;
                        if (!string.IsNullOrWhiteSpace(lib.AllowedExtensions))
                        {
                            var parsed = lib.AllowedExtensions
                                .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(e => e.Trim())
                                .Where(e => !string.IsNullOrEmpty(e))
                                .Select(e => e.StartsWith(".") ? e.ToLowerInvariant() : "." + e.ToLowerInvariant());

                            allowedExtsSet = new HashSet<string>(parsed, StringComparer.OrdinalIgnoreCase);
                        }

                        foreach (var file in allFiles)
                        {
                            string ext = Path.GetExtension(file);

                            if (allowedExtsSet != null && allowedExtsSet.Count > 0)
                            {
                                if (!allowedExtsSet.Contains(ext))
                                    continue;
                            }
                            else
                            {
                                if (!ExtensionHelper.IsSupportedFile(file, targetType))
                                    continue;
                            }

                            string relPath = Path.GetRelativePath(lib.Path, file);
                            fileDict[relPath] = (relPath, file);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Erro ao indexar {lib.Path}: {ex.Message}");
                    }

                    libraryIndexes[lib] = fileDict;
                });
            });

            // Comparar as listas afim de identificar arquivos que estão em uma biblioteca e não estão nas outras
            var generatedItems = new List<SyncItemModel>();

            foreach (var sourceLib in relevantPaths)
            {
                if (!libraryIndexes.TryGetValue(sourceLib, out var sourceFiles))
                    continue;

                var otherLibs = relevantPaths.Where(p => p != sourceLib).ToList();

                foreach (var kvp in sourceFiles)
                {
                    string relPath = kvp.Key;
                    var (actualRelPath, fullSourcePath) = kvp.Value;
                    string fileName = Path.GetFileName(fullSourcePath);

                    foreach (var destLib in otherLibs)
                    {
                        if (!libraryIndexes.TryGetValue(destLib, out var destFiles))
                            continue;

                        // Se o arquivo não existir na biblioteca de destino
                        if (!destFiles.ContainsKey(relPath))
                        {
                            string destFullDir = Path.GetDirectoryName(Path.Combine(destLib.Path, actualRelPath)) ?? destLib.Path;
                            string destFullPath = Path.Combine(destLib.Path, actualRelPath);

                            generatedItems.Add(new SyncItemModel
                            {
                                SourceLibraryName = string.IsNullOrWhiteSpace(sourceLib.Description) ? Path.GetFileName(sourceLib.Path) : sourceLib.Description,
                                SourceFilePath = fullSourcePath,
                                SourceFileName = fileName,
                                DestinationLibraryName = string.IsNullOrWhiteSpace(destLib.Description) ? Path.GetFileName(destLib.Path) : destLib.Description,
                                DestinationFolderPath = destFullDir,
                                DestinationFilePath = destFullPath,
                                StatusTooltip = $"Falta em: {destLib.Description} ({destFullDir})"
                            });
                        }
                    }
                }
            }

            foreach (var item in generatedItems)
            {
                SyncItems.Add(item);
            }

            StatusMessage = SyncItems.Count == 0
                ? "Todas as bibliotecas estão sincronizadas!"
                : $"Análise concluída: {SyncItems.Count} arquivo(s) precisam de sincronização.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro durante a análise: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            IsAnalyzing = false;
        }
    }

    [RelayCommand]
    public async Task ProcessSyncAsync()
    {
        var pendingToCopy = SyncItems.Where(i => !i.IsCopied).ToList();
        if (!pendingToCopy.Any())
        {
            StatusMessage = "Nenhum arquivo pendente para sincronizar.";
            return;
        }

        IsBusy = true;
        IsProcessing = true;
        ProgressTotal = pendingToCopy.Count;
        ProgressValue = 0;
        StatusMessage = "Iniciando cópia dos arquivos...";

        int successCount = 0;
        int errorCount = 0;

        await Task.Run(async () =>
        {
            foreach (var item in pendingToCopy)
            {
                try
                {
                    if (!File.Exists(item.SourceFilePath))
                    {
                        item.HasError = true;
                        item.RowColor = "#EF4444";
                        item.StatusTooltip = "Arquivo de origem não encontrado.";
                        errorCount++;
                        continue;
                    }

                    if (!Directory.Exists(item.DestinationFolderPath))
                    {
                        Directory.CreateDirectory(item.DestinationFolderPath);
                    }

                    // Copia o arquivo
                    File.Copy(item.SourceFilePath, item.DestinationFilePath, overwrite: true);

                    item.IsCopied = true;
                    item.HasError = false;
                    item.RowColor = "Transparent";
                    item.StatusTooltip = "Copiado com sucesso.";
                    successCount++;
                }
                catch (Exception ex)
                {
                    item.HasError = true;
                    item.RowColor = "#EF4444";
                    item.StatusTooltip = $"Erro ao copiar: {ex.Message}";
                    errorCount++;
                }

                ProgressValue++;
            }
        });

        IsBusy = false;
        IsProcessing = false;
        StatusMessage = $"Processamento concluído. {successCount} copiado(s), {errorCount} erro(s).";
    }

    [RelayCommand]
    public void RemoveSyncItem(SyncItemModel item)
    {
        if (item != null && SyncItems.Contains(item))
        {
            SyncItems.Remove(item);
            StatusMessage = $"Item removido da fila ({SyncItems.Count} restantes).";
        }
    }

    [RelayCommand]
    public void ClearSyncItems()
    {
        SyncItems.Clear();
        ProgressValue = 0;
        ProgressTotal = 0;
        StatusMessage = "Fila de sincronização limpa.";
    }
}
