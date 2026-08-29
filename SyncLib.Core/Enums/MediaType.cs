namespace SyncLib.Core.Enums;

public enum MediaType
{
    MangaPortugues,
    MangaJapones,
    MangaIngles,
    EbookPortugues,
    EbookIngles,
    EbookJapones
}

public static class MediaTypeExtensions
{
    public static string ToDisplayName(this MediaType mediaType) => mediaType switch
    {
        MediaType.MangaPortugues => "MANGA PORTUGUÊS",
        MediaType.MangaJapones => "MANGA JAPONÊS",
        MediaType.MangaIngles => "MANGA INGLÊS",
        MediaType.EbookPortugues => "EBOOK PORTUGUÊS",
        MediaType.EbookIngles => "EBOOK INGLÊS",
        MediaType.EbookJapones => "EBOOK JAPONÊS",
        _ => mediaType.ToString()
    };
}
