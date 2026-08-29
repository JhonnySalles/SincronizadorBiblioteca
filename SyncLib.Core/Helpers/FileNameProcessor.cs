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
    public static ProcessedFileNameResult Process(string originalFileName, MediaType mediaType, IEnumerable<NamingPattern>? savedPatterns = null, string? customSuffix = null)
    {
        string extension = Path.GetExtension(originalFileName);
        string nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);

        // Regex para capturar a série e o número do volume (ex: Vol. 18, Vol 18, Volume 18)
        var match = Regex.Match(nameWithoutExt, @"^(?<series>.+?)[,\s_]*\b[Vv]ol(?:ume)?\.?\s*(?<vol>\d+)", RegexOptions.IgnoreCase);

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

        string cleanedSeries = seriesRaw
            .Replace("_", " - ")
            .Replace(",", "");

        cleanedSeries = Regex.Replace(cleanedSeries, @"\s+", " ").Trim();
        cleanedSeries = Regex.Replace(cleanedSeries, @"\s*-\s*", " - ").Trim(' ', '-');

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
            finalSeriesName = ExtractSeriesFromFormatted(finalName, volumeNumber, extension, suffixPart);
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

    private static string ExtractSeriesFromFormatted(string formattedFileName, int? volumeNumber, string extension, string customSuffixPart)
    {
        string name = Path.GetFileNameWithoutExtension(formattedFileName);
        if (!string.IsNullOrEmpty(customSuffixPart) && name.EndsWith(customSuffixPart, StringComparison.OrdinalIgnoreCase))
        {
            name = name.Substring(0, name.Length - customSuffixPart.Length);
        }

        var match = Regex.Match(name, @"^(?<series>.+?)\s*(?:-\s*|[Vv]ol(?:ume)?\.?\s*)\d+", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups["series"].Value.Trim(' ', '-');
        }

        return name.Trim();
    }

    private static string Normalize(string input)
    {
        return Regex.Replace(input, @"[\s,_\-]", "").ToLowerInvariant();
    }
}
