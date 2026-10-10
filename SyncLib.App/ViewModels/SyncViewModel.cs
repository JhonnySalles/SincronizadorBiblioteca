using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SyncLib.App.Models;
using SyncLib.Core.Entities;
using SyncLib.Core.Enums;
using SyncLib.Core.Helpers;
using SyncLib.Core.Services.Storage;
using SyncLib.Infrastructure.Data;
using SyncLib.Infrastructure.Services.Storage;
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
    private readonly IStorageProviderFactory _storageFactory = new StorageProviderFactory();

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

    private ConfigurationPath ToEntity(PathDisplayModel model)
    {
        return new ConfigurationPath
        {
            Id = model.Id,
            Path = model.Path,
            MediaType = model.MediaType,
            ConnectionType = model.ConnectionType,
            ServerHost = model.ServerHost,
            ServerPort = model.ServerPort,
            Username = model.Username,
            Password = model.Password,
            Description = model.Description,
            CustomSuffix = model.CustomSuffix,
            CustomFolderSuffix = model.CustomFolderSuffix,
            AllowedExtensions = model.AllowedExtensions,
            IncludesSubfolders = model.IncludesSubfolders
        };
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
            .Where(p => p.MediaType == targetType && p.ExistsOnDisk)
            .ToList();

        if (relevantPaths.Count < 2)
        {
            StatusMessage = "É necessário ter pelo menos 2 bibliotecas ativas configuradas para comparar.";
            return;
        }

        IsBusy = true;
        IsAnalyzing = true;
        StatusMessage = "Analisando bibliotecas em paralelo...";
        SyncItems.Clear();

        try
        {
            var libraryIndexes = new ConcurrentDictionary<PathDisplayModel, List<IndexedFileInfo>>();

            await Task.Run(async () =>
            {
                var tasks = relevantPaths.Select(async lib =>
                {
                    var fileList = new List<IndexedFileInfo>();
                    var entity = ToEntity(lib);
                    using var storage = _storageFactory.CreateProvider(entity);

                    try
                    {
                        var allFiles = await storage.GetFilesAsync(lib.Path, "*.*", recursive: lib.IncludesSubfolders);

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

                            string relPath = file.StartsWith(lib.Path, StringComparison.OrdinalIgnoreCase)
                                ? file.Substring(lib.Path.Length).TrimStart('\\', '/')
                                : Path.GetFileName(file);

                            string fileName = Path.GetFileName(file);
                            var (series, vol) = FileNameProcessor.ExtractSeriesAndVolumeFromFinalFileName(fileName);
                            string normSeries = Normalize(series);
                            string volKey = vol.HasValue ? $"VOL:{normSeries}:{vol.Value}" : $"FILE:{normSeries}:{Normalize(fileName)}";
                            string dirPath = Path.GetDirectoryName(file)?.Replace('\\', '/') ?? lib.Path;

                            fileList.Add(new IndexedFileInfo
                            {
                                RelativePath = relPath,
                                FullPath = file,
                                FileName = fileName,
                                SeriesName = series,
                                VolumeNumber = vol,
                                NormalizedSeries = normSeries,
                                VolumeKey = volKey,
                                DirectoryPath = dirPath
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Erro ao indexar {lib.Path}: {ex.Message}");
                    }

                    libraryIndexes[lib] = fileList;
                });

                await Task.WhenAll(tasks);
            });

            // Comparar as listas a fim de identificar arquivos que estão em uma biblioteca e não estão nas outras
            var generatedItems = new List<SyncItemModel>();

            foreach (var sourceLib in relevantPaths)
            {
                if (!libraryIndexes.TryGetValue(sourceLib, out var sourceFiles))
                    continue;

                var otherLibs = relevantPaths.Where(p => p != sourceLib).ToList();

                foreach (var sourceFile in sourceFiles)
                {
                    foreach (var destLib in otherLibs)
                    {
                        if (!libraryIndexes.TryGetValue(destLib, out var destFiles))
                            continue;

                        bool existsInDest = destFiles.Any(d =>
                            d.VolumeKey.Equals(sourceFile.VolumeKey, StringComparison.OrdinalIgnoreCase) ||
                            d.RelativePath.Equals(sourceFile.RelativePath, StringComparison.OrdinalIgnoreCase));

                        if (!existsInDest)
                        {
                            string destFullDir;

                            if (!destLib.IncludesSubfolders)
                            {
                                destFullDir = destLib.Path;
                            }
                            else
                            {
                                var existingSameSeries = destFiles.FirstOrDefault(d => d.NormalizedSeries.Equals(sourceFile.NormalizedSeries, StringComparison.OrdinalIgnoreCase));
                                if (existingSameSeries != null && !string.IsNullOrEmpty(existingSameSeries.DirectoryPath))
                                {
                                    destFullDir = existingSameSeries.DirectoryPath;
                                }
                                else
                                {
                                    destFullDir = await FindOrProposeDestinationFolderAsync(destLib, sourceFile.SeriesName, destLib.CustomFolderSuffix);
                                }
                            }

                            var processed = FileNameProcessor.Process(sourceFile.FileName, targetType, null, destLib.CustomSuffix);
                            string destFileName = processed.FormattedFileName;
                            string destFullPath = destLib.ConnectionType == StorageConnectionType.Ftp
                                ? $"{destFullDir.TrimEnd('/')}/{destFileName}"
                                : Path.Combine(destFullDir, destFileName);

                            generatedItems.Add(new SyncItemModel
                            {
                                SourceLibraryName = string.IsNullOrWhiteSpace(sourceLib.Description) ? Path.GetFileName(sourceLib.Path) : sourceLib.Description,
                                SourceConfig = sourceLib,
                                DestinationConfig = destLib,
                                SourceFilePath = sourceFile.FullPath,
                                SourceFileName = sourceFile.FileName,
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
        StatusMessage = "Iniciando sincronização dos arquivos...";

        int successCount = 0;
        int errorCount = 0;

        await Task.Run(async () =>
        {
            foreach (var item in pendingToCopy)
            {
                try
                {
                    var srcEntity = item.SourceConfig != null ? ToEntity(item.SourceConfig) : new ConfigurationPath { Path = Path.GetDirectoryName(item.SourceFilePath) ?? "" };
                    var destEntity = item.DestinationConfig != null ? ToEntity(item.DestinationConfig) : new ConfigurationPath { Path = item.DestinationFolderPath };

                    using var sourceStorage = _storageFactory.CreateProvider(srcEntity);
                    using var destStorage = _storageFactory.CreateProvider(destEntity);

                    bool srcExists = await sourceStorage.FileExistsAsync(item.SourceFilePath);
                    if (!srcExists)
                    {
                        item.HasError = true;
                        item.RowColor = "#EF4444";
                        item.StatusTooltip = "Arquivo de origem não encontrado no storage.";
                        errorCount++;
                        continue;
                    }

                    if (!await destStorage.DirectoryExistsAsync(item.DestinationFolderPath))
                    {
                        await destStorage.CreateDirectoryAsync(item.DestinationFolderPath);
                    }

                    // Transfere o arquivo usando a abstração de storage (Local, Rede ou FTP)
                    await _storageFactory.TransferFileAsync(sourceStorage, item.SourceFilePath, destStorage, item.DestinationFilePath, overwrite: true);

                    item.IsCopied = true;
                    item.HasError = false;
                    item.RowColor = "Transparent";
                    item.StatusTooltip = "Sincronizado com sucesso.";
                    successCount++;
                }
                catch (Exception ex)
                {
                    item.HasError = true;
                    item.RowColor = "#EF4444";
                    item.StatusTooltip = $"Erro ao transferir: {ex.Message}";
                    errorCount++;
                }

                ProgressValue++;
            }
        });

        IsBusy = false;
        IsProcessing = false;
        StatusMessage = $"Processamento concluído. {successCount} copiado(s)/enviado(s), {errorCount} erro(s).";
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

    private async Task<string> FindOrProposeDestinationFolderAsync(PathDisplayModel destLib, string seriesName, string? customFolderSuffix)
    {
        var entity = ToEntity(destLib);
        using var storage = _storageFactory.CreateProvider(entity);

        if (await storage.DirectoryExistsAsync(destLib.Path))
        {
            try
            {
                var subdirs = await storage.GetDirectoriesAsync(destLib.Path);
                string normSeries = Normalize(seriesName);

                // 1. Procura pasta contendo arquivos da série
                foreach (var dir in subdirs)
                {
                    var files = await storage.GetFilesAsync(dir);
                    foreach (var f in files)
                    {
                        var (extractedSeries, extractedVol) = FileNameProcessor.ExtractSeriesAndVolumeFromFinalFileName(Path.GetFileName(f));
                        string normExtracted = Normalize(extractedSeries);
                        if (!string.IsNullOrEmpty(normExtracted) &&
                            (normExtracted == normSeries || normExtracted.StartsWith(normSeries) || normSeries.StartsWith(normExtracted)))
                        {
                            return dir;
                        }
                    }
                }

                // 2. Procura pasta por nome normalizado
                foreach (var dir in subdirs)
                {
                    string folderName = Path.GetFileName(dir.TrimEnd('/', '\\'));
                    string normFolder = Normalize(folderName);
                    if (normFolder == normSeries || normFolder.StartsWith(normSeries) || normSeries.StartsWith(normFolder))
                    {
                        return dir;
                    }
                }
            }
            catch { }
        }

        string cleanSeries = FileNameProcessor.CleanSeriesName(seriesName);
        string folderSuffix = !string.IsNullOrWhiteSpace(customFolderSuffix) ? $" {customFolderSuffix.Trim()}" : "";
        string folderNameOnly = $"{cleanSeries}{folderSuffix}";

        return destLib.ConnectionType == StorageConnectionType.Ftp
            ? $"{destLib.Path.TrimEnd('/')}/{folderNameOnly}"
            : Path.Combine(destLib.Path, folderNameOnly);
    }

    private static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(input, @"[\s,_\-'""‘’“”]", "").ToLowerInvariant();
    }
}

public class IndexedFileInfo
{
    public string RelativePath { get; set; } = string.Empty;
    public string FullPath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string SeriesName { get; set; } = string.Empty;
    public decimal? VolumeNumber { get; set; }
    public string NormalizedSeries { get; set; } = string.Empty;
    public string VolumeKey { get; set; } = string.Empty;
    public string DirectoryPath { get; set; } = string.Empty;
}
