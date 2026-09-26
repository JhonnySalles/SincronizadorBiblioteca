using SyncLib.Core.Entities;
using SyncLib.Core.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SyncLib.Core.Helpers;

public class ProcessedFileNameResult
{
    public string OriginalRawSeries { get; set; } = string.Empty;
    public string SeriesName { get; set; } = string.Empty;
    public int? VolumeNumber { get; set; }
    public string FormattedFileName { get; set; } = string.Empty;
}

public static class FileNameProcessor
{
    public static string CleanSeriesName(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string cleaned = input
            .Replace("_", " - ")
            .Replace(",", "")
            .Replace("'", "")
            .Replace("\"", "")
            .Replace("’", "")
            .Replace("‘", "")
            .Replace("“", "")
            .Replace("”", "");

        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        cleaned = Regex.Replace(cleaned, @"\s*-\s*", " - ").Trim(' ', '-');
        return cleaned;
    }

    public static (string SeriesName, int? VolumeNumber) ExtractSeriesAndVolumeFromFinalFileName(string finalFileName)
    {
        if (string.IsNullOrWhiteSpace(finalFileName)) return (string.Empty, null);

        string nameWithoutExt = Path.GetFileNameWithoutExtension(finalFileName);

        // 1. Tenta encontrar pelo último traço antes do volume (ganância garante o último hífen)
        var matchWithDash = Regex.Match(nameWithoutExt, @"^(?<series>.+)\s*-\s*\b[Vv]ol(?:ume)?\.?\s*(?<vol>\d+)", RegexOptions.IgnoreCase);
        if (matchWithDash.Success)
        {
            string series = CleanSeriesName(matchWithDash.Groups["series"].Value);
            int? vol = int.TryParse(matchWithDash.Groups["vol"].Value, out int v) ? v : null;
            return (series, vol);
        }

        // 2. Fallback para volume sem traço antes
        var matchNoDash = Regex.Match(nameWithoutExt, @"^(?<series>.+?)\s+\b[Vv]ol(?:ume)?\.?\s*(?<vol>\d+)", RegexOptions.IgnoreCase);
        if (matchNoDash.Success)
        {
            string series = CleanSeriesName(matchNoDash.Groups["series"].Value);
            int? vol = int.TryParse(matchNoDash.Groups["vol"].Value, out int v) ? v : null;
            return (series, vol);
        }

        // 3. Sem indicador de volume detectado
        return (CleanSeriesName(nameWithoutExt), null);
    }

    public static ProcessedFileNameResult Process(string originalFileName, MediaType mediaType, IEnumerable<NamingPattern>? savedPatterns = null, string? customSuffix = null)
    {
        string extension = Path.GetExtension(originalFileName);
        string nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);

        // Regex para capturar a série e o número do volume (priorizando o último traço antes de Vol/Volume)
        var matchWithDash = Regex.Match(nameWithoutExt, @"^(?<series>.+)\s*-\s*\b[Vv]ol(?:ume)?\.?\s*(?<vol>\d+)", RegexOptions.IgnoreCase);
        var matchGeneral = Regex.Match(nameWithoutExt, @"^(?<series>.+?)[,\s_]*\b[Vv]ol(?:ume)?\.?\s*(?<vol>\d+)", RegexOptions.IgnoreCase);

        var match = matchWithDash.Success ? matchWithDash : matchGeneral;

        string seriesRaw;
        int? volumeNumber = null;

        if (match.Success)
        {
            seriesRaw = match.Groups["series"].Value.Trim();
            if (int.TryParse(match.Groups["vol"].Value, out int v))
            {
                volumeNumber = v;
            }
        }
        else
        {
            seriesRaw = nameWithoutExt.Trim();
        }

        string cleanedSeries = CleanSeriesName(seriesRaw);

        string suffixPart = !string.IsNullOrWhiteSpace(customSuffix) ? $" {customSuffix.Trim()}" : string.Empty;

        // Verifica se há um padrão salvo para esta série original
        NamingPattern? pattern = null;
        if (savedPatterns != null)
        {
            string normRaw = Normalize(seriesRaw);
            string normClean = Normalize(cleanedSeries);

            pattern = savedPatterns.FirstOrDefault(p =>
                Normalize(p.OriginalRawSeries) == normRaw ||
                Normalize(p.OriginalRawSeries) == normClean);
        }

        string finalName;
        string finalSeriesName = cleanedSeries;

        if (pattern != null && !string.IsNullOrWhiteSpace(pattern.CustomTemplate))
        {
            finalName = ApplyTemplate(pattern.CustomTemplate, volumeNumber, suffixPart, extension);
            finalName = CleanQuotes(finalName);
            var extracted = ExtractSeriesAndVolumeFromFinalFileName(finalName);
            finalSeriesName = extracted.SeriesName;
        }
        else
        {
            if (volumeNumber.HasValue)
            {
                string formattedVol = volumeNumber.Value.ToString("D2");
                finalName = $"{cleanedSeries} - Volume {formattedVol}{suffixPart}{extension}";
            }
            else
            {
                finalName = $"{cleanedSeries}{suffixPart}{extension}";
            }
        }

        return new ProcessedFileNameResult
        {
            OriginalRawSeries = seriesRaw,
            SeriesName = finalSeriesName,
            VolumeNumber = volumeNumber,
            FormattedFileName = finalName
        };
    }

    public static string ApplyTemplate(string template, int? volumeNumber, string customSuffixPart, string extension)
    {
        string result = template;
        if (volumeNumber.HasValue)
        {
            result = result.Replace("{Volume:D2}", volumeNumber.Value.ToString("D2"));
            result = result.Replace("{Volume}", volumeNumber.Value.ToString());
        }
        result = result.Replace("{LangSuffix}", customSuffixPart);
        result = result.Replace("{Extension}", extension);
        return result;
    }

    private static string CleanQuotes(string text)
    {
        return text
            .Replace("'", "")
            .Replace("\"", "")
            .Replace("’", "")
            .Replace("‘", "")
            .Replace("“", "")
            .Replace("”", "");
    }

    public static string Normalize(string input)
    {
        return Regex.Replace(input, @"[\s,_\-'""‘’“”]", "").ToLowerInvariant();
    }
}
