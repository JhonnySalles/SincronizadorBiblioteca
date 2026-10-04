using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SyncLib.Core.Models.Api;

namespace SyncLib.Core.Services;

public static class MetadataExtractor
{
    public static (OpfDto? Opf, string? RawContent) ExtractOpf(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return (null, null);

            using var archive = ZipFile.OpenRead(filePath);
            
            // Find OPF file
            var opfEntry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith(".opf", StringComparison.OrdinalIgnoreCase));
            if (opfEntry == null) return (null, null);

            using var stream = opfEntry.Open();
            using var reader = new StreamReader(stream);
            string rawContent = reader.ReadToEnd();

            var opfDto = new OpfDto();
            
            try 
            {
                var xdoc = XDocument.Parse(rawContent);
                var opfNs = xdoc.Root?.GetDefaultNamespace() ?? "http://www.idpf.org/2007/opf";
                var dcNs = XNamespace.Get("http://purl.org/dc/elements/1.1/");
                var metadata = xdoc.Root?.Element(opfNs + "metadata");

                if (metadata != null)
                {
                    opfDto.Title = metadata.Element(dcNs + "title")?.Value ?? string.Empty;
                    opfDto.Creator = metadata.Element(dcNs + "creator")?.Value;
                    opfDto.Publisher = metadata.Element(dcNs + "publisher")?.Value;
                    opfDto.Language = metadata.Element(dcNs + "language")?.Value ?? "en";
                    opfDto.Description = metadata.Element(dcNs + "description")?.Value;
                    opfDto.DatePublished = metadata.Element(dcNs + "date")?.Value;

                    // Novel defaults to Series or Title (since we added it as string field)
                    opfDto.Novel = opfDto.Title;

                    // Series extraction cascade
                    // 1. belongs-to-collection
                    var belongsToCollection = metadata.Elements(opfNs + "meta")
                        .FirstOrDefault(x => x.Attribute("property")?.Value == "belongs-to-collection")?.Value;
                    
                    if (!string.IsNullOrWhiteSpace(belongsToCollection))
                    {
                        opfDto.Series = belongsToCollection;
                    }
                    else
                    {
                        // 2. dc:title regex (before ", Vol." or " - Volume ")
                        var matchTitle = Regex.Match(opfDto.Title, @"^(?<series>.+?)(?:,\s*Vol\.|\s*-\s*Volume\s*)", RegexOptions.IgnoreCase);
                        if (matchTitle.Success)
                            opfDto.Series = matchTitle.Groups["series"].Value.Trim();
                        else
                        {
                            // 3. Filename before "Volume"
                            var fileName = Path.GetFileNameWithoutExtension(filePath);
                            var matchFile = Regex.Match(fileName, @"^(?<series>.+?)(?:\s*-\s*Volume\s*|\s+Volume\s+)", RegexOptions.IgnoreCase);
                            if (matchFile.Success)
                                opfDto.Series = matchFile.Groups["series"].Value.Trim();
                        }
                    }

                    // Volume extraction cascade
                    // 1. group-position
                    var groupPosition = metadata.Elements(opfNs + "meta")
                        .FirstOrDefault(x => x.Attribute("property")?.Value == "group-position")?.Value;
                    
                    if (!string.IsNullOrWhiteSpace(groupPosition) && float.TryParse(groupPosition, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float v1))
                    {
                        opfDto.Volume = v1;
                    }
                    else
                    {
                        // 2. dc:title regex
                        var matchTitle = Regex.Match(opfDto.Title, @"\bVol(?:ume)?\.?\s*(?<vol>\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
                        if (matchTitle.Success && float.TryParse(matchTitle.Groups["vol"].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float v2))
                        {
                            opfDto.Volume = v2;
                        }
                        else
                        {
                            // 3. Filename regex
                            var fileName = Path.GetFileNameWithoutExtension(filePath);
                            var matchFile = Regex.Match(fileName, @"\bVol(?:ume)?\.?\s*(?<vol>\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
                            if (matchFile.Success && float.TryParse(matchFile.Groups["vol"].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float v3))
                            {
                                opfDto.Volume = v3;
                            }
                        }
                    }
                }
            } 
            catch { /* Ignore XML parse errors */ }

            return (opfDto, rawContent);
        }
        catch
        {
            return (null, null);
        }
    }

    public static (ComicInfoDto? ComicInfo, string? RawContent) ExtractComicInfo(string filePath)
    {
        try
        {
            if (!File.Exists(filePath)) return (null, null);

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            if (ext != ".cbz" && ext != ".zip" && ext != ".cbr") return (null, null);
            
            // Assume CBR might also be handled by ZipFile if it's actually zip renamed, or skip if unsupported. 
            // C# standard ZipFile doesn't support RAR. We will wrap in try/catch.
            using var archive = ZipFile.OpenRead(filePath);
            
            var comicInfoEntry = archive.Entries.FirstOrDefault(e => e.FullName.Equals("ComicInfo.xml", StringComparison.OrdinalIgnoreCase));
            if (comicInfoEntry == null) return (null, null);

            using var stream = comicInfoEntry.Open();
            using var reader = new StreamReader(stream);
            string rawContent = reader.ReadToEnd();

            var dto = new ComicInfoDto();
            dto.Comic = Path.GetFileName(filePath);
            
            try
            {
                var xdoc = XDocument.Parse(rawContent);
                var root = xdoc.Root;
                if (root != null)
                {
                    dto.Title = root.Element("Title")?.Value ?? string.Empty;
                    dto.Series = root.Element("Series")?.Value ?? string.Empty;
                    
                    if (float.TryParse(root.Element("Number")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float num))
                        dto.Number = num;

                    dto.Writer = root.Element("Writer")?.Value;
                    dto.Publisher = root.Element("Publisher")?.Value;
                    dto.Genre = root.Element("Genre")?.Value;
                    dto.LanguageISO = root.Element("LanguageISO")?.Value ?? "pt";
                    dto.Summary = root.Element("Summary")?.Value;
                    dto.Manga = root.Element("Manga")?.Value ?? "Yes";

                    // Volume extraction cascade
                    // 1. <Volume>
                    if (float.TryParse(root.Element("Volume")?.Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float v1))
                    {
                        dto.Volume = v1;
                    }
                    else if (dto.Number > 0) // 2. <Number>
                    {
                        dto.Volume = dto.Number;
                    }
                    else 
                    {
                        // 3. Filename
                        var fileName = Path.GetFileNameWithoutExtension(filePath);
                        var matchFile = Regex.Match(fileName, @"\bVol(?:ume)?\.?\s*(?<vol>\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
                        if (matchFile.Success && float.TryParse(matchFile.Groups["vol"].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out float v3))
                        {
                            dto.Volume = v3;
                        }
                    }
                }
            }
            catch { /* Ignore XML parse errors */ }

            return (dto, rawContent);
        }
        catch
        {
            return (null, null);
        }
    }
}
