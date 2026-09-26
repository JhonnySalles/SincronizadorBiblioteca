using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using SyncLib.App.Models;
using SyncLib.Core.Entities;
using SyncLib.Core.Enums;
using SyncLib.Infrastructure.Data;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace SyncLib.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    [ObservableProperty]
    private string _inputPath = string.Empty;

    [ObservableProperty]
    private string _inputDescription = string.Empty;

    [ObservableProperty]
    private string _inputCustomSuffix = string.Empty;

    [ObservableProperty]
    private string _inputCustomFolderSuffix = string.Empty;

    [ObservableProperty]
    private string _inputAllowedExtensions = string.Empty;

    [ObservableProperty]
    private bool _inputIncludesSubfolders;

    [ObservableProperty]
    private MediaTypeOption? _selectedMediaTypeOption;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public List<MediaTypeOption> MediaTypeOptions { get; } = Enum.GetValues<MediaType>()
        .Select(t => new MediaTypeOption { Type = t })
        .ToList();

    public ObservableCollection<PathDisplayModel> ConfiguredPaths { get; } = new();

    public SettingsViewModel()
    {
        SelectedMediaTypeOption = MediaTypeOptions.FirstOrDefault();
        _ = LoadConfiguredPathsAsync();
    }

    [RelayCommand]
    private async Task LoadConfiguredPathsAsync()
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

            StatusMessage = "Configurações carregadas do banco de dados com sucesso.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao carregar configurações: {ex.Message}";
        }
    }

    [RelayCommand]
    private void AddPath()
    {
        if (string.IsNullOrWhiteSpace(InputPath))
        {
            StatusMessage = "Por favor, informe um caminho válido.";
            return;
        }

        if (SelectedMediaTypeOption == null)
        {
            StatusMessage = "Por favor, selecione um tipo de mídia.";
            return;
        }

        var trimmedPath = InputPath.Trim();
        if (ConfiguredPaths.Any(p => p.Path.Equals(trimmedPath, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = "Este caminho já foi cadastrado nas configurações.";
            return;
        }

        var model = new PathDisplayModel
        {
            Path = InputPath.Trim(),
            MediaType = SelectedMediaTypeOption.Type,
            Description = InputDescription.Trim(),
            CustomSuffix = InputCustomSuffix.Trim(),
            CustomFolderSuffix = InputCustomFolderSuffix.Trim(),
            AllowedExtensions = InputAllowedExtensions.Trim(),
            IncludesSubfolders = InputIncludesSubfolders
        };
        model.CheckStatus();

        ConfiguredPaths.Add(model);

        // Resetar inputs
        InputPath = string.Empty;
        InputDescription = string.Empty;
        InputCustomSuffix = string.Empty;
        InputCustomFolderSuffix = string.Empty;
        InputAllowedExtensions = string.Empty;
        InputIncludesSubfolders = false;

        StatusMessage = "Caminho inserido na lista (clique em Salvar para persistir no banco).";
    }

    [RelayCommand]
    private void DeletePath(PathDisplayModel? item)
    {
        if (item != null && ConfiguredPaths.Contains(item))
        {
            ConfiguredPaths.Remove(item);
            StatusMessage = "Caminho removido da lista.";
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        try
        {
            using var db = new AppDbContext();
            await db.Database.MigrateAsync();

            var existing = await db.ConfigurationPaths.ToListAsync();
            db.ConfigurationPaths.RemoveRange(existing);

            foreach (var model in ConfiguredPaths)
            {
                db.ConfigurationPaths.Add(new ConfigurationPath
                {
                    Id = model.Id,
                    Path = model.Path,
                    MediaType = model.MediaType,
                    Description = model.Description,
                    CustomSuffix = model.CustomSuffix,
                    CustomFolderSuffix = model.CustomFolderSuffix,
                    AllowedExtensions = model.AllowedExtensions,
                    IncludesSubfolders = model.IncludesSubfolders
                });
            }

            await db.SaveChangesAsync();
            StatusMessage = "Configurações salvas com sucesso!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erro ao salvar no banco: {ex.Message}";
        }
    }

    [RelayCommand]
    private void RefreshStatuses()
    {
        foreach (var path in ConfiguredPaths)
        {
            path.CheckStatus();
        }
        StatusMessage = "Status dos caminhos atualizados.";
    }

    public void SortPaths(string? columnTag, bool ascending)
    {
        if (string.IsNullOrEmpty(columnTag) || ConfiguredPaths.Count <= 1) return;

        List<PathDisplayModel> sorted = columnTag switch
        {
            "MediaType" or "Tipo" => ascending ? ConfiguredPaths.OrderBy(p => p.MediaTypeDisplayName).ToList() : ConfiguredPaths.OrderByDescending(p => p.MediaTypeDisplayName).ToList(),
            "Description" or "Descrição" => ascending ? ConfiguredPaths.OrderBy(p => p.Description).ToList() : ConfiguredPaths.OrderByDescending(p => p.Description).ToList(),
            "CustomSuffix" or "Sufixo Arquivo" => ascending ? ConfiguredPaths.OrderBy(p => p.CustomSuffix).ToList() : ConfiguredPaths.OrderByDescending(p => p.CustomSuffix).ToList(),
            "CustomFolderSuffix" or "Sufixo Pasta" => ascending ? ConfiguredPaths.OrderBy(p => p.CustomFolderSuffix).ToList() : ConfiguredPaths.OrderByDescending(p => p.CustomFolderSuffix).ToList(),
            "AllowedExtensions" or "Extensões" => ascending ? ConfiguredPaths.OrderBy(p => p.AllowedExtensions).ToList() : ConfiguredPaths.OrderByDescending(p => p.AllowedExtensions).ToList(),
            "Path" or "Caminho" => ascending ? ConfiguredPaths.OrderBy(p => p.Path).ToList() : ConfiguredPaths.OrderByDescending(p => p.Path).ToList(),
            "IncludesSubfolders" or "Sub-pastas" => ascending ? ConfiguredPaths.OrderBy(p => p.IncludesSubfolders).ToList() : ConfiguredPaths.OrderByDescending(p => p.IncludesSubfolders).ToList(),
            "Status" or "StatusText" => ascending ? ConfiguredPaths.OrderBy(p => p.StatusText).ToList() : ConfiguredPaths.OrderByDescending(p => p.StatusText).ToList(),
            _ => ConfiguredPaths.ToList()
        };

        ConfiguredPaths.Clear();
        foreach (var item in sorted)
        {
            ConfiguredPaths.Add(item);
        }
    }
}
