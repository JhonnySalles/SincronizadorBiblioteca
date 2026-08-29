using SyncLib.Core.Enums;
using System;
using System.IO;
using System.Text.RegularExpressions;

namespace SyncLib.Core.Helpers;

public class ProcessedFileNameResult
{
    public string SeriesName { get; set; } = string.Empty;
    public int? VolumeNumber { get; set; }
    public string FormattedFileName { get; set; } = string.Empty;
}

public static class FileNameProcessor
{
    public static ProcessedFileNameResult Process(string originalFileName, MediaType mediaType)
    {
        string extension = Path.GetExtension(originalFileName);
        string nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);

        // Regex para capturar a série e o número do volume (ex: Vol. 18, Vol 18, Volume 18)
        var match = Regex.Match(nameWithoutExt, @"^(?<series>.+?)[,\s_]*\b[Vv]ol(?:ume)?\.?\s*(?<vol>\d+)", RegexOptions.IgnoreCase);

        string seriesRaw;
        int? volumeNumber = null;

        if (match.Success)
        {
            seriesRaw = match.Groups["series"].Value;
            if (int.TryParse(match.Groups["vol"].Value, out int v))
            {
                volumeNumber = v;
            }
        }
        else
        {
            seriesRaw = nameWithoutExt;
        }

        // Regras de substituição no nome da série:
        // 1. Underline '_' trocado por ' - '
        // 2. Vírgula ',' removida
        // 3. Limpeza de múltiplos espaços
        string cleanedSeries = seriesRaw
            .Replace("_", " - ")
            .Replace(",", "");

        cleanedSeries = Regex.Replace(cleanedSeries, @"\s+", " ").Trim();
        cleanedSeries = Regex.Replace(cleanedSeries, @"\s*-\s*", " - ").Trim(' ', '-');

        // Sufixo de idioma
        string langSuffix = mediaType switch
        {
            MediaType.MangaIngles or MediaType.EbookIngles => " (Eng)",
            MediaType.MangaJapones or MediaType.EbookJapones => " (Jap)",
            _ => string.Empty
        };

        string finalName;
        if (volumeNumber.HasValue)
        {
            string formattedVol = volumeNumber.Value.ToString("D2");
            finalName = $"{cleanedSeries} - Volume {formattedVol}{langSuffix}{extension}";
        }
        else
        {
            finalName = $"{cleanedSeries}{langSuffix}{extension}";
        }

        return new ProcessedFileNameResult
        {
            SeriesName = cleanedSeries,
            VolumeNumber = volumeNumber,
            FormattedFileName = finalName
        };
    }
}
