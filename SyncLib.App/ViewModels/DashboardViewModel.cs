using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SyncLib.App.Models;
using SyncLib.Core.Entities;
using SyncLib.Core.Enums;
using SyncLib.Core.Helpers;
using SyncLib.Core.Services;
using SyncLib.Core.Services.Storage;
using SyncLib.Infrastructure.Data;
using SyncLib.Infrastructure.Services.Storage;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace SyncLib.App.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly IStorageProviderFactory _storageFactory = new StorageProviderFactory();
    private readonly IJapaneseTextNormalizer _japaneseNormalizer = new KawazuJapaneseTextNormalizer();
    private List<DirectoryCache> _inMemoryDirectoryCache = new();

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

    [ObservableProperty]
    private double _copyProgress;

    [ObservableProperty]
    private double _copyTotal;

    [ObservableProperty]
    private bool _isCopying;

    [ObservableProperty]
    private bool _isApiOnline;

    [ObservableProperty]
    private string _apiStatusTooltip = "Verificando API...";

    [ObservableProperty]
    private string _apiStatusColor = "#888888";

    private readonly SyncLib.Core.Services.ApiSyncService _apiSyncService = new();
    private readonly List<NamingPattern> _namingPatterns = new();
    private bool _isSyncingFinalName;
    private bool _isSyncingTargetDirectory;

    public ObservableCollection<FileItemModel> PendingFiles { get; } = new();

    private void AttachItemEvents(FileItemModel item)
    {
        item.PropertyChanged -= FileItem_PropertyChanged;
        item.PropertyChanged += FileItem_PropertyChanged;
    }

    private void FileItem_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FileItemModel.FinalFileName) && sender is FileItemModel changedItem)
        {
            if (_isSyncingFinalName) return;
            _isSyncingFinalName = true;
            try
            {
                foreach (var item in PendingFiles)
                {
                    if (item != changedItem && string.Equals(item.FileName, changedItem.FileName, StringComparison.OrdinalIgnoreCase))
                    {
                        item.FinalFileName = changedItem.FinalFileName;
                    }
                }

                if (!string.IsNullOrEmpty(changedItem.OriginalRawSeries) &&
                    !string.Equals(changedItem.FinalFileName, changedItem.OriginalAutoFileName, StringComparison.Ordinal))
                {
                    SaveOrUpdateNamingPattern(changedItem);
                }
            }
            finally
            {
                _isSyncingFinalName = false;
            }
        }
        else if ((e.PropertyName == nameof(FileItemModel.DisplayTargetDirectory) || e.PropertyName == nameof(FileItemModel.TargetDirectory)) && sender is FileItemModel changedFolderItem)
        {
            ValidateFolderExistence(changedFolderItem);
            
            if (_isSyncingTargetDirectory) return;
            
            if (e.PropertyName == nameof(FileItemModel.TargetDirectory) && changedFolderItem.IsCustomTarget)
            {
                _isSyncingTargetDirectory = true;
                try
                {
                    var sourceSeries = FileNameProcessor.CleanSeriesName(changedFolderItem.OriginalRawSeries);
                    foreach (var item in PendingFiles)
                    {
                        if (item != changedFolderItem && 
                            item.DestinationFolder == changedFolderItem.DestinationFolder &&
                            !item.IsCustomTarget)
                        {
                            var itemSeries = FileNameProcessor.CleanSeriesName(item.OriginalRawSeries);
                            if (string.Equals(itemSeries, sourceSeries, StringComparison.OrdinalIgnoreCase))
                            {
                                item.TargetDirectory = changedFolderItem.TargetDirectory;
                                item.IsCustomTarget = true;
                                item.RowColor = "Transparent";
                                item.StatusTooltip = $"Pasta personalizada (replicada): {changedFolderItem.TargetDirectory}";
                            }
                        }
                    }
                }
                finally
                {
                    _isSyncingTargetDirectory = false;
                }
            }
        }
    }

    public void ValidateFolderExistence(FileItemModel item)
    {
        if (string.IsNullOrWhiteSpace(item.TargetDirectory)) return;

        if (Directory.Exists(item.TargetDirectory))
        {
            item.RowColor = "Transparent";
            item.StatusTooltip = $"OK: Pasta localizada ({Path.GetFileName(item.TargetDirectory)}).";
            if (!string.IsNullOrEmpty(item.SeriesFolderName))
            {
                UpdateDirectoryCache(item.SeriesFolderName, item.TargetDirectory, item.DestinationFolder);
            }
        }
        else
        {
            item.RowColor = "#FFF97316"; // Laranja
            item.StatusTooltip = $"Pasta da série não existe no destino. Será criada ao copiar: {item.TargetDirectory}";
        }
    }

    private void SaveOrUpdateNamingPattern(FileItemModel item)
    {
        string rawSeries = item.OriginalRawSeries;
        if (string.IsNullOrWhiteSpace(rawSeries) || string.IsNullOrWhiteSpace(item.FinalFileName)) return;

        string extension = Path.GetExtension(item.FinalFileName);
        string template = item.FinalFileName;

        if (item.VolumeNumber.HasValue)
        {
            string volD2 = item.VolumeNumber.Value.ToString("00.##", System.Globalization.CultureInfo.InvariantCulture);
            string volSimple = item.VolumeNumber.Value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);

            if (template.Contains(volD2))
            {
                template = ReplaceLast(template, volD2, "{Volume:D2}");
            }
            else if (template.Contains(volSimple))
            {
                template = ReplaceLast(template, volSimple, "{Volume}");
            }
        }

        if (!string.IsNullOrEmpty(extension))
        {
            template = ReplaceLast(template, extension, "{Extension}");
        }

        string normRaw = System.Text.RegularExpressions.Regex.Replace(rawSeries, @"[\s,_\-'""‘’“”]", "").ToLowerInvariant();
        var existing = _namingPatterns.FirstOrDefault(p =>
            System.Text.RegularExpressions.Regex.Replace(p.OriginalRawSeries, @"[\s,_\-'""‘’“”]", "").ToLowerInvariant() == normRaw);

        if (existing != null)
        {
            existing.CustomTemplate = template;
        }
        else
        {
            var newPattern = new NamingPattern
            {
                OriginalRawSeries = rawSeries,
                CustomTemplate = template
            };
            _namingPatterns.Add(newPattern);
        }

        _ = SaveNamingPatternToDbAsync(rawSeries, template);
    }

    private string ReplaceLast(string text, string search, string replace)
    {
        int pos = text.LastIndexOf(search, StringComparison.OrdinalIgnoreCase);
        if (pos < 0) return text;
        return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
    }

    private async Task SaveNamingPatternToDbAsync(string originalRawSeries, string customTemplate)
    {
        try
        {
            using var db = new AppDbContext();
            string normRaw = System.Text.RegularExpressions.Regex.Replace(originalRawSeries, @"[\s,_\-'""‘’“”]", "").ToLowerInvariant();
            var dbEntries = await db.NamingPatterns.ToListAsync();
            var dbMatch = dbEntries.FirstOrDefault(p =>
                System.Text.RegularExpressions.Regex.Replace(p.OriginalRawSeries, @"[\s,_\-'""‘’“”]", "").ToLowerInvariant() == normRaw);

            if (dbMatch != null)
            {
                dbMatch.CustomTemplate = customTemplate;
            }
            else
            {
                db.NamingPatterns.Add(new NamingPattern
                {
                    OriginalRawSeries = originalRawSeries,
                    CustomTemplate = customTemplate
                });
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao salvar NamingPattern: {ex.Message}");
        }
    }

    private bool _sortAscendingBySource = true;
    private bool _sortAscendingByDestination = true;

    public DashboardViewModel()
    {
        SelectedMediaTypeOption = MediaTypeOptions.FirstOrDefault();
        _ = InitializeDashboardAsync();
    }

    private async Task InitializeDashboardAsync()
    {
        await CheckApiStatusAsync();
        await LoadConfiguredPathsAsync();
        await EnsureDirectoryCacheAsync();
        await LoadNamingPatternsAsync();
    }

    [RelayCommand]
    private async Task CheckApiStatusAsync()
    {
        ApiStatusTooltip = "Verificando API...";
        IsApiOnline = await _apiSyncService.IsApiOnlineAsync();
        
        if (IsApiOnline)
        {
            ApiStatusTooltip = "API Online";
            ApiStatusColor = "#22C55E"; // Green
        }
        else
        {
            ApiStatusTooltip = "API Offline";
            ApiStatusColor = "#EF4444"; // Red
        }
    }

    private async Task LoadNamingPatternsAsync()
    {
        try
        {
            using var db = new AppDbContext();
            var patterns = await db.NamingPatterns.ToListAsync();
            _namingPatterns.Clear();
            _namingPatterns.AddRange(patterns);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao carregar NamingPatterns: {ex.Message}");
        }
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
            await db.Database.ExecuteSqlRawAsync(@"
                CREATE TABLE IF NOT EXISTS ""DirectoryCaches"" (
                    ""Id"" TEXT NOT NULL CONSTRAINT ""PK_DirectoryCaches"" PRIMARY KEY,
                    ""RootPath"" TEXT NOT NULL,
                    ""SeriesName"" TEXT NOT NULL,
                    ""FolderPath"" TEXT NOT NULL,
                    ""MediaType"" TEXT NOT NULL,
                    ""LastScanned"" TEXT NOT NULL
                );");

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
                    var destEntity = ToEntity(pathConfig);
                    using var storage = _storageFactory.CreateProvider(destEntity);
                    if (await storage.DirectoryExistsAsync(root))
                    {
                        var subdirs = await storage.GetDirectoriesAsync(root);
                        var oldEntries = existingCaches
                            .Where(c => c.RootPath.Equals(root, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        
                        db.DirectoryCaches.RemoveRange(oldEntries);

                        var now = DateTime.Now;
                        foreach (var dir in subdirs)
                        {
                            var folderName = Path.GetFileName(dir.TrimEnd('/', '\\'));
                            if (!string.IsNullOrEmpty(folderName))
                            {
                                db.DirectoryCaches.Add(new DirectoryCache
                                {
                                    RootPath = root,
                                    SeriesName = folderName,
                                    FolderPath = dir,
                                    MediaType = pathConfig.MediaType,
                                    LastScanned = now
                                });
                            }
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

    public async void AddFiles(IEnumerable<string> filePaths)
    {
        await AddFilesAsync(filePaths);
    }

    public async Task AddFilesAsync(IEnumerable<string> filePaths)
    {
        if (!PendingFiles.Any())
        {
            var firstFile = GetFirstFilePath(filePaths);
            if (!string.IsNullOrEmpty(firstFile))
            {
                bool isCurrentCompatible = SelectedMediaTypeOption != null && ExtensionHelper.IsSupportedFile(firstFile, SelectedMediaTypeOption.Type);
                if (!isCurrentCompatible)
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
                await ProcessFileBatchAsync(subFiles, targetMediaType, matchingPaths, addedCountRef => addedCount = addedCountRef, ignoredCountRef => ignoredCount = ignoredCountRef, addedCount, ignoredCount);
            }
            else if (File.Exists(filePath))
            {
                await ProcessFileBatchAsync(new[] { filePath }, targetMediaType, matchingPaths, addedCountRef => addedCount = addedCountRef, ignoredCountRef => ignoredCount = ignoredCountRef, addedCount, ignoredCount);
            }
        }

        // Reavaliar possíveis falsos vermelhos (quando o Vol 1 entrou depois na fila)
        foreach (var item in PendingFiles.Where(p => p.RowColor == "#EF4444").ToList())
        {
            bool hasVol1InQueue = PendingFiles.Any(p => 
                p != item && 
                p.VolumeNumber == 1 && 
                p.TargetDirectory == item.TargetDirectory);

            if (hasVol1InQueue)
            {
                item.RowColor = "#F97316"; 
                item.StatusTooltip = $"Série nova na fila: Volume {item.VolumeNumber} será copiado para a nova pasta.";
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
        bool hasExt = Path.HasExtension(cleanPath);

        if (!hasExt)
        {
            if (Directory.Exists(cleanPath))
            {
                AddFiles(new[] { cleanPath });
            }
            else
            {
                StatusMessage = $"A pasta informada não foi encontrada: {cleanPath}";
            }
        }
        else
        {
            if (File.Exists(cleanPath))
            {
                AddFiles(new[] { cleanPath });
            }
            else if (Directory.Exists(cleanPath))
            {
                AddFiles(new[] { cleanPath });
            }
            else
            {
                StatusMessage = $"O arquivo informado não foi encontrado: {cleanPath}";
            }
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

    private async Task ProcessFileBatchAsync(IEnumerable<string> files, MediaType targetMediaType, List<PathDisplayModel> matchingPaths, Action<int> setAdded, Action<int> setIgnored, int currentAdded, int currentIgnored)
    {
        int addedCount = currentAdded;
        int ignoredCount = currentIgnored;

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

            bool isJapanese = _japaneseNormalizer.IsJapanese(fileName);
            string? japaneseAutoFileName = null;
            string? japaneseSeriesName = null;
            decimal? japaneseVolume = null;

            if (isJapanese)
            {
                japaneseAutoFileName = await _japaneseNormalizer.NormalizeToRomajiAsync(fileName);
                var extracted = FileNameProcessor.ExtractSeriesAndVolumeFromFinalFileName(japaneseAutoFileName);
                japaneseSeriesName = extracted.SeriesName;
                japaneseVolume = extracted.VolumeNumber;
            }

            foreach (var dest in matchingPaths)
            {
                string seriesName;
                decimal? volumeNumber;
                string originalRawSeries;
                string originalAutoFileName;
                string finalFileName;

                if (isJapanese && japaneseAutoFileName != null)
                {
                    seriesName = japaneseSeriesName ?? string.Empty;
                    volumeNumber = japaneseVolume;
                    originalRawSeries = Path.GetFileNameWithoutExtension(fileName);
                    originalAutoFileName = japaneseAutoFileName;
                    finalFileName = japaneseAutoFileName;
                }
                else
                {
                    var processed = FileNameProcessor.Process(fileName, targetMediaType, _namingPatterns, dest.CustomSuffix);
                    seriesName = processed.SeriesName;
                    volumeNumber = processed.VolumeNumber;
                    originalRawSeries = processed.OriginalRawSeries;
                    originalAutoFileName = processed.FormattedFileName;
                    finalFileName = dest.IncludesSubfolders ? processed.FormattedFileName : fileName;
                }

                var item = new FileItemModel
                {
                    FileName = fileName,
                    FilePath = file,
                    DestinationFolder = dest.Path,
                    MediaTypeDisplayName = targetMediaType.ToDisplayName(),
                    IsSubfoldersActive = dest.IncludesSubfolders,
                    VolumeNumber = volumeNumber,
                    OriginalRawSeries = originalRawSeries,
                    OriginalAutoFileName = originalAutoFileName,
                    FinalFileName = finalFileName
                };

                EvaluateDestinationForItem(item, seriesName, volumeNumber, dest, targetMediaType);

                AttachItemEvents(item);
                PendingFiles.Add(item);
                addedCount++;
            }
        }

        setAdded(addedCount);
        setIgnored(ignoredCount);
    }

    private HashSet<decimal> GetExistingVolumesForSeries(string targetDirectory, string seriesName, bool isSubfolders, FileItemModel? currentItem = null)
    {
        var volumes = new HashSet<decimal>();
        string normSeries = NormalizeForComparison(seriesName);

        if (Directory.Exists(targetDirectory))
        {
            try
            {
                var files = Directory.GetFiles(targetDirectory);
                foreach (var f in files)
                {
                    var fileName = Path.GetFileName(f);
                    var (extractedSeries, extractedVol) = FileNameProcessor.ExtractSeriesAndVolumeFromFinalFileName(fileName);
                    
                    if (extractedVol.HasValue)
                    {
                        if (isSubfolders)
                        {
                            volumes.Add(extractedVol.Value);
                        }
                        else
                        {
                            string normExtracted = NormalizeForComparison(extractedSeries);
                            if (normExtracted == normSeries || normExtracted.StartsWith(normSeries) || normSeries.StartsWith(normExtracted))
                            {
                                volumes.Add(extractedVol.Value);
                            }
                        }
                    }
                }
            }
            catch { }
        }

        foreach (var pending in PendingFiles)
        {
            if (pending != currentItem &&
                pending.VolumeNumber.HasValue &&
                string.Equals(pending.TargetDirectory, targetDirectory, StringComparison.OrdinalIgnoreCase))
            {
                if (isSubfolders)
                {
                    volumes.Add(pending.VolumeNumber.Value);
                }
                else
                {
                    string normPendingSeries = NormalizeForComparison(pending.OriginalRawSeries);
                    if (normPendingSeries == normSeries || normPendingSeries.StartsWith(normSeries) || normSeries.StartsWith(normPendingSeries))
                    {
                        volumes.Add(pending.VolumeNumber.Value);
                    }
                }
            }
        }

        return volumes;
    }

    private string? FindVolume01FileName(string folderPath, string seriesName)
    {
        if (Directory.Exists(folderPath))
        {
            try
            {
                var files = Directory.GetFiles(folderPath);
                // 1. Prioridade: busca arquivo que corresponde ao Volume 1
                foreach (var f in files)
                {
                    var fileName = Path.GetFileName(f);
                    var (_, extractedVol) = FileNameProcessor.ExtractSeriesAndVolumeFromFinalFileName(fileName);
                    if (extractedVol == 1)
                    {
                        return fileName;
                    }
                }

                // 2. Caso não tenha vol 1 explícito, pega o primeiro arquivo válido de mídia na pasta
                var firstSupported = files.FirstOrDefault(f => !f.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(f).StartsWith("."));
                if (firstSupported != null)
                {
                    return Path.GetFileName(firstSupported);
                }
            }
            catch { }
        }
        return null;
    }

    private void EvaluateDestinationForItem(FileItemModel item, string seriesName, decimal? volumeNumber, PathDisplayModel? destConfig, MediaType targetMediaType)
    {
        bool isSubfolders = destConfig != null ? destConfig.IncludesSubfolders : item.IsSubfoldersActive;
        string destPath = destConfig != null ? destConfig.Path : item.DestinationFolder;
        string? customFolderSuffix = destConfig?.CustomFolderSuffix;

        item.IsSubfoldersActive = isSubfolders;
        item.DestinationFolder = destPath;
        item.VolumeNumber = volumeNumber;

        if (!isSubfolders)
        {
            item.TargetDirectory = destPath;
            item.SeriesFolderName = string.Empty;

            if (volumeNumber.HasValue && volumeNumber.Value > 1)
            {
                var existingVolumes = GetExistingVolumesForSeries(destPath, seriesName, isSubfolders: false, item);
                var missingVolumes = new List<int>();
                for (int i = 1; i < volumeNumber.Value; i++)
                {
                    if (!existingVolumes.Contains(i))
                    {
                        missingVolumes.Add(i);
                    }
                }

                if (missingVolumes.Any())
                {
                    item.RowColor = "#EAB308"; // Amarelo (Falta de sequência)
                    item.StatusTooltip = $"Atenção: Volumes anteriores ausentes na biblioteca: {string.Join(", ", missingVolumes.Select(v => $"Vol. {v:D2}"))}.";
                }
                else
                {
                    item.RowColor = "Transparent";
                    item.StatusTooltip = "OK: Arquivo pronto para cópia na raiz.";
                }
            }
            else
            {
                item.RowColor = "Transparent";
                item.StatusTooltip = "OK: Arquivo pronto para cópia na raiz.";
            }
            return;
        }

        var matchedCache = FindMatchingDirectoryCache(destPath, seriesName, targetMediaType, customFolderSuffix);
        bool folderExists = destConfig?.ConnectionType == StorageConnectionType.Ftp 
            ? matchedCache != null 
            : (matchedCache != null && Directory.Exists(matchedCache.FolderPath));

        if (matchedCache != null && folderExists)
        {
            item.SeriesFolderName = matchedCache.SeriesName;
            item.TargetDirectory = matchedCache.FolderPath;

            var vol1FileName = FindVolume01FileName(matchedCache.FolderPath, seriesName);
            string vol1Info = !string.IsNullOrEmpty(vol1FileName) ? $" - {vol1FileName}" : string.Empty;

            if (volumeNumber.HasValue && volumeNumber.Value > 1)
            {
                var existingVolumes = GetExistingVolumesForSeries(matchedCache.FolderPath, seriesName, isSubfolders: true, item);
                var missingVolumes = new List<int>();
                for (int i = 1; i < volumeNumber.Value; i++)
                {
                    if (!existingVolumes.Contains(i))
                    {
                        missingVolumes.Add(i);
                    }
                }

                if (missingVolumes.Any())
                {
                    item.RowColor = "#EAB308"; // Amarelo (Falta de sequência)
                    item.StatusTooltip = $"Atenção: Volumes anteriores ausentes: {string.Join(", ", missingVolumes.Select(v => $"Vol. {v:D2}"))}.";
                }
                else
                {
                    item.RowColor = "Transparent";
                    item.StatusTooltip = $"OK: Pasta localizada ({matchedCache.SeriesName}){vol1Info}";
                }
            }
            else
            {
                item.RowColor = "Transparent";
                item.StatusTooltip = $"OK: Pasta localizada ({matchedCache.SeriesName}){vol1Info}";
            }
        }
        else
        {
            bool isEbook = targetMediaType == MediaType.EbookPortugues ||
                           targetMediaType == MediaType.EbookIngles ||
                           targetMediaType == MediaType.EbookJapones;

            string folderSuffix = !string.IsNullOrWhiteSpace(customFolderSuffix)
                ? (customFolderSuffix.StartsWith(" ") ? customFolderSuffix : $" {customFolderSuffix.Trim()}")
                : string.Empty;

            string cleanSeries = FileNameProcessor.CleanSeriesName(seriesName);
            string baseSeriesName = cleanSeries;
            if (!string.IsNullOrEmpty(folderSuffix) && !baseSeriesName.EndsWith(folderSuffix.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                baseSeriesName = $"{baseSeriesName}{folderSuffix}";
            }

            item.SeriesFolderName = baseSeriesName;
            var proposedFolder = destConfig?.ConnectionType == StorageConnectionType.Ftp
                ? $"{destPath.TrimEnd('/')}/{baseSeriesName}"
                : Path.Combine(destPath, baseSeriesName);
            item.TargetDirectory = proposedFolder;

            if (volumeNumber == 1)
            {
                item.RowColor = "#F97316"; // Laranja (Nova pasta)
                item.StatusTooltip = $"Nova série: Pasta '{baseSeriesName}' será criada no destino.";
            }
            else
            {
                bool hasVol1InQueue = PendingFiles.Any(p => 
                    p != item && 
                    p.VolumeNumber == 1 && 
                    p.TargetDirectory == proposedFolder);

                if (hasVol1InQueue)
                {
                    item.RowColor = "#F97316"; // Laranja (Nova pasta na fila)
                    item.StatusTooltip = $"Série nova na fila: Volume {volumeNumber} será copiado para a nova pasta.";
                }
                else
                {
                    item.RowColor = "#EF4444"; // Vermelho (Pasta ou Vol 01 não encontrado)
                    item.StatusTooltip = $"Atenção: Pasta da série ou Volume 01 não encontrado no destino para o Volume {volumeNumber}!";
                }
            }
        }
    }

    [RelayCommand]
    public void ReanalyzeFileItem(FileItemModel? item)
    {
        if (item == null) return;

        var targetType = SelectedMediaTypeOption?.Type ?? MediaType.EbookPortugues;

        // Encontra todos os itens na fila que correspondem ao mesmo arquivo de origem
        var relatedItems = PendingFiles
            .Where(p => string.Equals(p.FilePath, item.FilePath, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(p.FileName, item.FileName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (!relatedItems.Any())
        {
            relatedItems.Add(item);
        }

        int count = 0;
        foreach (var rel in relatedItems)
        {
            // Extrai série e volume a partir do FinalFileName atual
            var (extractedSeries, extractedVol) = FileNameProcessor.ExtractSeriesAndVolumeFromFinalFileName(rel.FinalFileName);

            rel.VolumeNumber = extractedVol;
            if (!string.IsNullOrEmpty(extractedSeries))
            {
                rel.OriginalRawSeries = extractedSeries;
            }

            var destConfig = ConfiguredPaths.FirstOrDefault(p => p.Path.Equals(rel.DestinationFolder, StringComparison.OrdinalIgnoreCase));
            EvaluateDestinationForItem(rel, extractedSeries, extractedVol, destConfig, targetType);

            count++;
        }

        StatusMessage = $"Destino reanalisado para '{item.FileName}' ({count} destino(s)).";
    }

    private decimal GetMaxVolumeInFolderAndQueue(string folderPath, string seriesName, FileItemModel? currentItem = null)
    {
        decimal maxVol = 0;

        if (Directory.Exists(folderPath))
        {
            try
            {
                var files = Directory.GetFiles(folderPath);
                foreach (var f in files)
                {
                    var filename = Path.GetFileName(f);
                    var volMatch = System.Text.RegularExpressions.Regex.Match(filename, @"\b[Vv]ol(?:ume)?\.?\s*(?<vol>\d+(?:\.\d+)?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    if (volMatch.Success && decimal.TryParse(volMatch.Groups["vol"].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal v))
                    {
                        if (v > maxVol) maxVol = v;
                    }
                }
            }
            catch { }
        }

        foreach (var pending in PendingFiles)
        {
            if (pending != currentItem &&
                pending.VolumeNumber.HasValue &&
                string.Equals(pending.TargetDirectory, folderPath, StringComparison.OrdinalIgnoreCase))
            {
                if (pending.VolumeNumber.Value > maxVol) maxVol = pending.VolumeNumber.Value;
            }
        }

        return maxVol;
    }

    partial void OnSelectedMediaTypeOptionChanged(MediaTypeOption? value)
    {
        PendingFiles.Clear();
    }

    private string StripKnownFolderSuffixes(string folderName)
    {
        string cleaned = folderName.Trim();
        if (cleaned.EndsWith("(Novel)", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned.Substring(0, cleaned.Length - "(Novel)".Length).Trim();
        else if (cleaned.EndsWith("[Novel]", StringComparison.OrdinalIgnoreCase))
            cleaned = cleaned.Substring(0, cleaned.Length - "[Novel]".Length).Trim();
        return cleaned;
    }

    private bool DirectoryContainsVolume01OrSeriesFiles(string dirPath, string seriesName)
    {
        if (!Directory.Exists(dirPath)) return false;

        try
        {
            var files = Directory.GetFiles(dirPath);
            string normSeries = NormalizeForComparison(seriesName);

            foreach (var file in files)
            {
                var (extractedSeries, extractedVol) = FileNameProcessor.ExtractSeriesAndVolumeFromFinalFileName(Path.GetFileName(file));
                string normExtracted = NormalizeForComparison(extractedSeries);

                if (!string.IsNullOrEmpty(normExtracted))
                {
                    bool isSeriesMatch = normExtracted == normSeries ||
                                         normExtracted.StartsWith(normSeries, StringComparison.OrdinalIgnoreCase) ||
                                         normSeries.StartsWith(normExtracted, StringComparison.OrdinalIgnoreCase);

                    if (isSeriesMatch && (extractedVol == 1 || normExtracted == normSeries))
                    {
                        return true;
                    }
                }
            }
        }
        catch { }

        return false;
    }

    private bool IsDirectoryMatch(string folderName, string seriesName, string? customFolderSuffix, MediaType targetMediaType)
    {
        string normSeries = NormalizeForComparison(seriesName);
        string normFolder = NormalizeForComparison(folderName);

        if (string.IsNullOrEmpty(normSeries) || string.IsNullOrEmpty(normFolder)) return false;

        // Nível 1: Igualdade exata normalizada
        if (normFolder == normSeries) return true;

        // Nível 1.1: Igualdade removendo sufixos padrão conhecidos da pasta
        string folderWithoutSuffix = StripKnownFolderSuffixes(folderName);
        string normFolderWithoutSuffix = NormalizeForComparison(folderWithoutSuffix);
        if (normFolderWithoutSuffix == normSeries) return true;

        if (!string.IsNullOrWhiteSpace(customFolderSuffix))
        {
            string normSeriesWithCustom = NormalizeForComparison($"{seriesName}{customFolderSuffix.Trim()}");
            if (normFolder == normSeriesWithCustom) return true;
        }

        bool isEbook = targetMediaType == MediaType.EbookPortugues ||
                       targetMediaType == MediaType.EbookIngles ||
                       targetMediaType == MediaType.EbookJapones;

        if (isEbook)
        {
            string normSeriesWithNovel = NormalizeForComparison($"{seriesName} (Novel)");
            if (normFolder == normSeriesWithNovel) return true;
        }

        // Nível 2: Correspondência por Prefixo (StartsWith) - para séries onde a pasta é o início do título longo
        if (normFolderWithoutSuffix.Length >= 6)
        {
            if (normSeries.StartsWith(normFolderWithoutSuffix, StringComparison.OrdinalIgnoreCase))
                return true;
            if (normFolderWithoutSuffix.StartsWith(normSeries, StringComparison.OrdinalIgnoreCase) && normSeries.Length >= 6)
                return true;
        }

        return false;
    }

    private DirectoryCache? FindMatchingDirectoryCache(string rootPath, string seriesName, MediaType targetMediaType, string? customFolderSuffix = null)
    {
        if (string.IsNullOrWhiteSpace(seriesName)) return null;

        // 1. Busca no cache pelo RootPath e MediaType especificados com matching estrito
        var match = _inMemoryDirectoryCache.FirstOrDefault(c =>
            c.RootPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase) &&
            c.MediaType == targetMediaType &&
            (IsDirectoryMatch(c.SeriesName, seriesName, customFolderSuffix, targetMediaType) ||
             (Directory.Exists(c.FolderPath) && DirectoryContainsVolume01OrSeriesFiles(c.FolderPath, seriesName))));

        // 2. Fallback de Disco na pasta raiz desta biblioteca específica
        if (match == null && Directory.Exists(rootPath))
        {
            try
            {
                var subdirs = Directory.GetDirectories(rootPath);
                
                // Prioridade 2.1: Pastas que contenham o Volume 01 ou arquivos da série
                foreach (var dir in subdirs)
                {
                    var folderName = Path.GetFileName(dir);
                    if (DirectoryContainsVolume01OrSeriesFiles(dir, seriesName))
                    {
                        match = new DirectoryCache
                        {
                            RootPath = rootPath,
                            SeriesName = folderName,
                            FolderPath = dir,
                            MediaType = targetMediaType,
                            LastScanned = DateTime.Now
                        };
                        _inMemoryDirectoryCache.Add(match);
                        _ = SaveDirectoryCacheToDbAsync(folderName, dir, rootPath, targetMediaType);
                        return match;
                    }
                }

                // Prioridade 2.2: Correspondência por nome da pasta
                foreach (var dir in subdirs)
                {
                    var folderName = Path.GetFileName(dir);

                    if (IsDirectoryMatch(folderName, seriesName, customFolderSuffix, targetMediaType))
                    {
                        match = new DirectoryCache
                        {
                            RootPath = rootPath,
                            SeriesName = folderName,
                            FolderPath = dir,
                            MediaType = targetMediaType,
                            LastScanned = DateTime.Now
                        };
                        _inMemoryDirectoryCache.Add(match);
                        _ = SaveDirectoryCacheToDbAsync(folderName, dir, rootPath, targetMediaType);
                        break;
                    }
                }
            }
            catch { }
        }

        return match;
    }

    public void UpdateDirectoryCache(string seriesName, string folderPath, string? explicitRootPath = null)
    {
        if (string.IsNullOrWhiteSpace(seriesName) || string.IsNullOrWhiteSpace(folderPath)) return;

        string cleanTarget = NormalizeForComparison(seriesName);
        var mediaType = SelectedMediaTypeOption?.Type ?? MediaType.EbookPortugues;
        string rootPath = !string.IsNullOrWhiteSpace(explicitRootPath)
            ? explicitRootPath
            : (Path.GetDirectoryName(folderPath) ?? string.Empty);

        var existing = _inMemoryDirectoryCache.FirstOrDefault(c =>
            c.RootPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase) &&
            c.MediaType == mediaType &&
            NormalizeForComparison(c.SeriesName).Equals(cleanTarget, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            existing.FolderPath = folderPath;
            existing.RootPath = rootPath;
            existing.LastScanned = DateTime.Now;
        }
        else
        {
            var newCache = new DirectoryCache
            {
                SeriesName = seriesName,
                FolderPath = folderPath,
                RootPath = rootPath,
                MediaType = mediaType,
                LastScanned = DateTime.Now
            };
            _inMemoryDirectoryCache.Add(newCache);
        }

        _ = SaveDirectoryCacheToDbAsync(seriesName, folderPath, rootPath, mediaType);
    }

    private async Task SaveDirectoryCacheToDbAsync(string seriesName, string folderPath, string rootPath, MediaType mediaType)
    {
        try
        {
            using var db = new AppDbContext();
            string cleanTarget = NormalizeForComparison(seriesName);
            var dbEntries = await db.DirectoryCaches.ToListAsync();
            var dbMatch = dbEntries.FirstOrDefault(c =>
                c.RootPath.Equals(rootPath, StringComparison.OrdinalIgnoreCase) &&
                c.MediaType == mediaType &&
                NormalizeForComparison(c.SeriesName).Equals(cleanTarget, StringComparison.OrdinalIgnoreCase));

            if (dbMatch != null)
            {
                dbMatch.FolderPath = folderPath;
                dbMatch.RootPath = rootPath;
                dbMatch.LastScanned = DateTime.Now;
            }
            else
            {
                db.DirectoryCaches.Add(new DirectoryCache
                {
                    SeriesName = seriesName,
                    FolderPath = folderPath,
                    RootPath = rootPath,
                    MediaType = mediaType,
                    LastScanned = DateTime.Now
                });
            }

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Erro ao salvar DirectoryCache: {ex.Message}");
        }
    }

    private string NormalizeForComparison(string text)
    {
        return System.Text.RegularExpressions.Regex.Replace(text, @"[\s,_\-'""‘’“”]", "").ToLowerInvariant();
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
            AttachItemEvents(item);
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

        IsCopying = true;
        CopyProgress = 0;
        CopyTotal = PendingFiles.Count;

        int copiedCount = 0;
        int errorCount = 0;
        var itemsToCopy = PendingFiles.ToList();

        // 1. Renomear arquivos de origem
        var itemsBySource = itemsToCopy.GroupBy(i => i.FilePath).ToList();
        foreach (var group in itemsBySource)
        {
            var firstItem = group.First();
            string oldPath = firstItem.FilePath;
            string dir = Path.GetDirectoryName(oldPath) ?? string.Empty;
            string newPath = Path.Combine(dir, firstItem.FinalFileName);

            if (!string.Equals(oldPath, newPath, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    if (File.Exists(newPath))
                    {
                        throw new IOException("Já existe um arquivo com o novo nome na pasta de origem.");
                    }
                    File.Move(oldPath, newPath);
                    foreach (var item in group)
                    {
                        item.FilePath = newPath;
                        item.FileName = firstItem.FinalFileName;
                        // Atualiza na grid
                    }
                }
                catch (Exception ex)
                {
                    foreach (var item in group)
                    {
                        item.RowColor = "#EF4444";
                        item.StatusTooltip = $"Erro ao renomear origem: {ex.Message}";
                    }
                    errorCount += group.Count();
                }
            }
        }

        // 2. Copiar
        var validItemsToCopy = itemsToCopy.Where(i => i.RowColor != "#EF4444").ToList();
        foreach (var item in validItemsToCopy)
        {
            try
            {
                StatusMessage = $"Copiando: {item.FinalFileName}... ({copiedCount + 1}/{(int)CopyTotal})";

                var destConfig = ConfiguredPaths.FirstOrDefault(p => p.Path.Equals(item.DestinationFolder, StringComparison.OrdinalIgnoreCase));
                var destEntity = destConfig != null ? ToEntity(destConfig) : new ConfigurationPath { Path = item.DestinationFolder };

                using var localProvider = new LocalStorageProvider();
                using var destStorage = _storageFactory.CreateProvider(destEntity);

                if (!await destStorage.DirectoryExistsAsync(item.TargetDirectory))
                {
                    await destStorage.CreateDirectoryAsync(item.TargetDirectory);

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

                var destFilePath = destEntity.ConnectionType == StorageConnectionType.Ftp
                    ? $"{item.TargetDirectory.TrimEnd('/')}/{item.FinalFileName}"
                    : Path.Combine(item.TargetDirectory, item.FinalFileName);

                if (await destStorage.FileExistsAsync(destFilePath))
                {
                    throw new IOException("O arquivo de destino já existe (sobrescrita bloqueada).");
                }

                await _storageFactory.TransferFileAsync(localProvider, item.FilePath, destStorage, destFilePath, overwrite: false);

                // Registra no log de cópia para rastreabilidade
                CopyLogger.LogCopy(item.FilePath, item.MediaTypeDisplayName, item.FinalFileName, item.TargetDirectory);

                if (IsApiOnline)
                {
                    StatusMessage = $"Sincronizando com a API: {item.FinalFileName}...";
                    try 
                    {
                        string lang = item.MediaTypeDisplayName.Contains("PORTUGUÊS", StringComparison.OrdinalIgnoreCase) ? "pt" :
                                      item.MediaTypeDisplayName.Contains("INGLÊS", StringComparison.OrdinalIgnoreCase) ? "en" :
                                      item.MediaTypeDisplayName.Contains("JAPONÊS", StringComparison.OrdinalIgnoreCase) ? "ja" : "pt";

                        if (item.MediaTypeDisplayName.Contains("EBOOK", StringComparison.OrdinalIgnoreCase))
                        {
                            await _apiSyncService.SyncEpubAsync(destFilePath, item.FinalFileName, lang);
                        }
                        else 
                        {
                            await _apiSyncService.SyncComicAsync(destFilePath, item.FinalFileName, lang);
                        }
                    }
                    catch (Exception apiEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"Erro na API: {apiEx.Message}");
                    }
                }

                item.CopiedFilePath = destFilePath;
                item.IsCopied = true;
                item.RowColor = "Transparent";
                copiedCount++;
            }
            catch (Exception ex)
            {
                errorCount++;
                item.RowColor = "#EF4444";
                item.StatusTooltip = $"Erro: {ex.Message}";
            }

            CopyProgress = copiedCount + errorCount;
        }

        IsCopying = false;

        if (errorCount == 0)
        {
            StatusMessage = $"Cópia concluída com sucesso! ({copiedCount} arquivo(s) copiados). {(IsApiOnline ? "API Sincronizada." : "API Offline.")}";
        }
        else
        {
            StatusMessage = $"{copiedCount} arquivo(s) copiados. {errorCount} falharam.";
        }
    }

    [RelayCommand]
    private void OpenCopiedFolder(FileItemModel? item)
    {
        if (item == null) return;

        try
        {
            if (!string.IsNullOrEmpty(item.CopiedFilePath) && File.Exists(item.CopiedFilePath))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select, \"{item.CopiedFilePath}\"",
                    UseShellExecute = true
                });
                return;
            }

            if (!string.IsNullOrEmpty(item.TargetDirectory) && Directory.Exists(item.TargetDirectory))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{item.TargetDirectory}\"",
                    UseShellExecute = true
                });
                return;
            }

            if (!string.IsNullOrEmpty(item.DestinationFolder) && Directory.Exists(item.DestinationFolder))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{item.DestinationFolder}\"",
                    UseShellExecute = true
                });
                return;
            }

            StatusMessage = "Pasta de destino não encontrada no disco.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao abrir pasta: {ex.Message}";
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
