using SyncLib.Core.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SyncLib.Core.Helpers;

public static class ExtensionHelper
{
    public static string ToFileExtension(this MangaExtension ext) => ext switch
    {
        MangaExtension.Cbr => ".cbr",
        MangaExtension.Cbz => ".cbz",
        MangaExtension.Cb7 => ".cb7",
        MangaExtension.Cbt => ".cbt",
        MangaExtension.Rar => ".rar",
        MangaExtension.Zip => ".zip",
        MangaExtension.Tar => ".tar",
        MangaExtension.SevenZip => ".7z",
        _ => string.Empty
    };

    public static string ToFileExtension(this EbookExtension ext) => ext switch
    {
        EbookExtension.Epub => ".epub",
        EbookExtension.Epub3 => ".epub3",
        EbookExtension.Pdf => ".pdf",
        EbookExtension.Mobi => ".mobi",
        EbookExtension.Djvu => ".djvu",
        EbookExtension.Fb2 => ".fb2",
        EbookExtension.Azw => ".azw",
        EbookExtension.Azw3 => ".azw3",
        EbookExtension.Doc => ".doc",
        EbookExtension.Tiff => ".tiff",
        EbookExtension.Odt => ".odt",
        EbookExtension.Opds => ".opds",
        _ => string.Empty
    };

    public static HashSet<string> GetSupportedExtensions(MediaType mediaType)
    {
        bool isManga = mediaType is MediaType.MangaPortugues or MediaType.MangaJapones or MediaType.MangaIngles;

        if (isManga)
        {
            return Enum.GetValues<MangaExtension>()
                .Select(e => e.ToFileExtension())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            return Enum.GetValues<EbookExtension>()
                .Select(e => e.ToFileExtension())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static bool IsSupportedFile(string filePath, MediaType mediaType)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        var ext = Path.GetExtension(filePath);
        if (string.IsNullOrEmpty(ext)) return false;

        var supported = GetSupportedExtensions(mediaType);
        return supported.Contains(ext);
    }
}
