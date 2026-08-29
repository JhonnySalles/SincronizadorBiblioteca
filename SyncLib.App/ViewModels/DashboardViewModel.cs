using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SyncLib.App.Models;
using SyncLib.Core.Entities;
using SyncLib.Core.Enums;
using SyncLib.Core.Helpers;
using SyncLib.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SyncLib.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private List<DirectoryCache> _inMemoryDirectoryCache = new();

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
    private string _searchPath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<FileItemModel> PendingFiles { get; } = new();

    private bool _sortAscendingBySource = true;
    private bool _sortAscendingByDestination = true;

    public DashboardViewModel()
    {
        SelectedMediaTypeOption = MediaTypeOptions.FirstOrDefault();
        _ = InitializeDashboardAsync();
    }

    private async Task InitializeDashboardAsync()
    {
        await LoadConfiguredPathsAsync();
        await EnsureDirectoryCacheAsync();
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

            foreach (var entity in entities)
            {
                var model = new PathDisplayModel(entity);
                ConfiguredPaths.Add(model);
            }

            OnPropertyChanged(nameof(DestinationPathsText));
            StatusMessage = "Caminhos atualizados.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao carregar caminhos: {ex.Message}";
        }
    }

    public async Task EnsureDirectoryCacheAsync()
    {
        try
        {
            using var db = new AppDbContext();
            await db.Database.MigrateAsync();

            var today = DateTime.Today;
            var existingCaches = await db.DirectoryCaches.ToListAsync();

            foreach (var pathConfig in ConfiguredPaths.Where(p => p.IncludesSubfolders && p.ExistsOnDisk))
            {
                var root = pathConfig.Path;
                var lastScanned = existingCaches
                    .Where(c => c.RootPath.Equals(root, StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.LastScanned)
                    .FirstOrDefault();

                if (lastScanned.Date < today)
                {
                    if (Directory.Exists(root))
                    {
                        var subdirs = Directory.GetDirectories(root);
                        var oldEntries = existingCaches
                            .Where(c => c.RootPath.Equals(root, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        
                        db.DirectoryCaches.RemoveRange(oldEntries);

                        var now = DateTime.Now;
                        foreach (var dir in subdirs)
                        {
                            var folderName = Path.GetFileName(dir);
                            db.DirectoryCaches.Add(new DirectoryCache
                            {
                                RootPath = root,
                                SeriesName = folderName,
                                FolderPath = dir,
                                MediaType = pathConfig.MediaType,
                                LastScanned = now
                            });
                        }

                        await db.SaveChangesAsync();
                    }
                }
            }

            _inMemoryDirectoryCache = await db.DirectoryCaches.ToListAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao atualizar cache de diretórios: {ex.Message}";
        }
    }

    public void AddFiles(IEnumerable<string> filePaths)
    {
        if (!PendingFiles.Any())
        {
            var firstFile = GetFirstFilePath(filePaths);
            if (!string.IsNullOrEmpty(firstFile))
            {
                var detectedType = DetectMediaTypeForFile(firstFile);
                if (detectedType.HasValue)
                {
                    var option = MediaTypeOptions.FirstOrDefault(o => o.Type == detectedType.Value);
                    if (option != null)
                    {
                        SelectedMediaTypeOption = option;
                    }
                }
            }
        }

        if (SelectedMediaTypeOption == null)
        {
            StatusMessage = "Selecione um tipo de processo antes de adicionar arquivos.";
            return;
        }

        var targetMediaType = SelectedMediaTypeOption.Type;
        var matchingPaths = ConfiguredPaths
            .Where(p => p.MediaType == targetMediaType)
            .ToList();

        if (!matchingPaths.Any())
        {
            StatusMessage = $"Atenção: Não há pastas de destino configuradas para {SelectedMediaTypeOption.DisplayName}.";
            return;
        }

        int addedCount = 0;
        int ignoredCount = 0;

        foreach (var filePath in filePaths)
        {
            if (Directory.Exists(filePath))
            {
                var subFiles = Directory.GetFiles(filePath, "*.*", SearchOption.TopDirectoryOnly);
                ProcessFileBatch(subFiles, targetMediaType, matchingPaths, ref addedCount, ref ignoredCount);
            }
            else if (File.Exists(filePath))
            {
                ProcessFileBatch(new[] { filePath }, targetMediaType, matchingPaths, ref addedCount, ref ignoredCount);
            }
        }

        SortPendingFilesBySourceInternal();

        if (ignoredCount > 0)
        {
            StatusMessage = $"Adicionados {addedCount} item(ns) à lista ({ignoredCount} ignorados por extensão não suportada para {SelectedMediaTypeOption.DisplayName}).";
        }
        else
        {
            StatusMessage = $"Adicionados {addedCount} item(ns) à lista.";
        }
    }

    private string? GetFirstFilePath(IEnumerable<string> filePaths)
    {
        foreach (var path in filePaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
            if (Directory.Exists(path))
            {
                var subFile = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly).FirstOrDefault();
                if (subFile != null)
                {
                    return subFile;
                }
            }
        }
        return null;
    }

    private MediaType? DetectMediaTypeForFile(string filePath)
    {
        // 1. Prioridade: Primeiro tipo entre os caminhos configurados que suporta a extensão do arquivo
        var matchingConfigured = ConfiguredPaths.FirstOrDefault(p => ExtensionHelper.IsSupportedFile(filePath, p.MediaType));
        if (matchingConfigured != null)
        {
            return matchingConfigured.MediaType;
        }

        // 2. Fallback: Primeiro tipo disponível nas opções que suporta a extensão
        var fallbackOption = MediaTypeOptions.FirstOrDefault(o => ExtensionHelper.IsSupportedFile(filePath, o.Type));
        if (fallbackOption != null)
        {
            return fallbackOption.Type;
        }

        return null;
    }

    [RelayCommand]
    public void AddFilesFromPath()
    {
        if (string.IsNullOrWhiteSpace(SearchPath))
        {
            StatusMessage = "Informe um caminho de arquivo ou pasta no campo de busca.";
            return;
        }

        var cleanPath = SearchPath.Trim().Trim('"').Trim();
        if (File.Exists(cleanPath) || Directory.Exists(cleanPath))
        {
            AddFiles(new[] { cleanPath });
        }
        else
        {
            StatusMessage = "O caminho informado não foi encontrado.";
        }
    }

    [RelayCommand]
    private async Task SelectCustomFolderAsync(FileItemModel? item)
    {
        if (item == null) return;

        var picker = new Windows.Storage.Pickers.FolderPicker();
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Desktop;
        picker.FileTypeFilter.Add("*");

        var window = (Microsoft.UI.Xaml.Application.Current as SyncLib_App.App)?.MainWindow;
        if (window != null)
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
        }

        var folder = await picker.PickSingleFolderAsync();
        if (folder != null)
        {
            item.IsCustomTarget = true;
            item.TargetDirectory = folder.Path;
            item.RowColor = "Transparent";
            item.StatusTooltip = $"Pasta personalizada selecionada: {folder.Path}";
        }
    }

    private void ProcessFileBatch(IEnumerable<string> files, MediaType targetMediaType, List<PathDisplayModel> matchingPaths, ref int addedCount, ref int ignoredCount)
    {
        foreach (var file in files)
        {
            if (!ExtensionHelper.IsSupportedFile(file, targetMediaType))
            {
                ignoredCount++;
                continue;
            }

            var fileName = Path.GetFileName(file);

            // Validação de duplicidade: valida o caminho inteiro do arquivo e o seu nome
            if (PendingFiles.Any(p => p.FilePath.Equals(file, StringComparison.OrdinalIgnoreCase) && 
                                      p.FileName.Equals(fileName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var processed = FileNameProcessor.Process(fileName, targetMediaType);

            foreach (var dest in matchingPaths)
            {
                var item = new FileItemModel
                {
                    FileName = fileName,
                    FilePath = file,
                    DestinationFolder = dest.Path,
                    MediaTypeDisplayName = targetMediaType.ToDisplayName(),
                    IsSubfoldersActive = dest.IncludesSubfolders,
                    VolumeNumber = processed.VolumeNumber
                };

                if (!dest.IncludesSubfolders)
                {
                    item.FinalFileName = fileName;
                    item.TargetDirectory = dest.Path;
                    item.RowColor = "Transparent";
                    item.StatusTooltip = "Cópia direta para a pasta de destino.";
                }
                else
                {
                    item.FinalFileName = processed.FormattedFileName;
                    var matchedCache = FindMatchingDirectoryCache(dest.Path, processed.SeriesName);

                    if (matchedCache != null)
                    {
                        item.SeriesFolderName = matchedCache.SeriesName;
                        item.TargetDirectory = matchedCache.FolderPath;
                        item.RowColor = "Transparent";
                        item.StatusTooltip = $"Pasta de destino localizada: {matchedCache.FolderPath}";
                    }
                    else
                    {
                        bool isEbook = targetMediaType == MediaType.EbookPortugues ||
                                       targetMediaType == MediaType.EbookIngles ||
                                       targetMediaType == MediaType.EbookJapones;

                        var seriesFolderName = isEbook && !processed.SeriesName.EndsWith("(Novel)", StringComparison.OrdinalIgnoreCase)
                            ? $"{processed.SeriesName} (Novel)"
                            : processed.SeriesName;

                        item.SeriesFolderName = seriesFolderName;
                        var proposedFolder = Path.Combine(dest.Path, seriesFolderName);
                        item.TargetDirectory = proposedFolder;

                        if (processed.VolumeNumber == 1)
                        {
                            item.RowColor = "#FFF97316"; // Laranja
                            item.StatusTooltip = $"Pasta da série não existe no destino. Será criada ao copiar: {proposedFolder}";
                        }
                        else
                        {
                            item.RowColor = "#EF4444"; // Vermelho
                            item.StatusTooltip = $"Atenção: Pasta da série não encontrada para Volume {processed.VolumeNumber}!";
                        }
                    }
                }

                PendingFiles.Add(item);
                addedCount++;
            }
        }
    }

    private DirectoryCache? FindMatchingDirectoryCache(string rootPath, string seriesName)
    {
        string cleanTarget = NormalizeForComparison(seriesName);

        return _inMemoryDirectoryCache.FirstOrDefault(c =>
            c.RootPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase) &&
            (NormalizeForComparison(c.SeriesName).Contains(cleanTarget, StringComparison.OrdinalIgnoreCase) ||
             cleanTarget.Contains(NormalizeForComparison(c.SeriesName), StringComparison.OrdinalIgnoreCase)));
    }

    private string NormalizeForComparison(string text)
    {
        return text.Replace("-", "").Replace("_", "").Replace(",", "").Replace(" ", "").ToLowerInvariant();
    }

    [RelayCommand]
    public void SortBySource()
    {
        _sortAscendingBySource = !_sortAscendingBySource;
        SortPendingFilesBySourceInternal();
    }

    private void SortPendingFilesBySourceInternal()
    {
        var sorted = _sortAscendingBySource
            ? PendingFiles.OrderBy(f => f.FileName).ToList()
            : PendingFiles.OrderByDescending(f => f.FileName).ToList();

        ReorderPendingFiles(sorted);
    }

    [RelayCommand]
    public void SortByDestination()
    {
        _sortAscendingByDestination = !_sortAscendingByDestination;

        var sorted = _sortAscendingByDestination
            ? PendingFiles.OrderBy(f => f.TargetDirectory).ThenBy(f => f.FinalFileName).ToList()
            : PendingFiles.OrderByDescending(f => f.TargetDirectory).ThenByDescending(f => f.FinalFileName).ToList();

        ReorderPendingFiles(sorted);
    }

    private void ReorderPendingFiles(List<FileItemModel> items)
    {
        PendingFiles.Clear();
        foreach (var item in items)
        {
            PendingFiles.Add(item);
        }
    }

    [RelayCommand]
    private async Task CopyFilesAsync()
    {
        if (!PendingFiles.Any())
        {
            StatusMessage = "Não há arquivos na fila para copiar.";
            return;
        }

        int copiedCount = 0;
        int errorCount = 0;
        var completedItems = new List<FileItemModel>();

        foreach (var item in PendingFiles.ToList())
        {
            try
            {
                if (!Directory.Exists(item.TargetDirectory))
                {
                    Directory.CreateDirectory(item.TargetDirectory);

                    var parentDir = Path.GetDirectoryName(item.TargetDirectory);
                    var folderName = Path.GetFileName(item.TargetDirectory);

                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        _inMemoryDirectoryCache.Add(new DirectoryCache
                        {
                            RootPath = parentDir,
                            SeriesName = folderName,
                            FolderPath = item.TargetDirectory,
                            LastScanned = DateTime.Now
                        });
                    }
                }

                var destFilePath = Path.Combine(item.TargetDirectory, item.FinalFileName);
                await Task.Run(() => File.Copy(item.FilePath, destFilePath, overwrite: true));

                // Registra no log de cópia para rastreabilidade
                CopyLogger.LogCopy(item.FilePath, item.MediaTypeDisplayName, item.FinalFileName, item.TargetDirectory);

                completedItems.Add(item);
                copiedCount++;
            }
            catch (Exception ex)
            {
                errorCount++;
                item.RowColor = "#EF4444";
                item.StatusTooltip = $"Erro ao copiar: {ex.Message}";
            }
        }

        foreach (var item in completedItems)
        {
            PendingFiles.Remove(item);
        }

        if (errorCount == 0)
        {
            StatusMessage = $"Cópia concluída com sucesso! ({copiedCount} arquivo(s) copiados).";
        }
        else
        {
            StatusMessage = $"{copiedCount} arquivo(s) copiados com sucesso. {errorCount} falharam.";
        }
    }

    [RelayCommand]
    private void RemovePendingFile(FileItemModel? item)
    {
        if (item != null && PendingFiles.Contains(item))
        {
            PendingFiles.Remove(item);
        }
    }

    [RelayCommand]
    private void ClearPendingFiles()
    {
        PendingFiles.Clear();
        StatusMessage = "Lista de arquivos limpa.";
    }
}
