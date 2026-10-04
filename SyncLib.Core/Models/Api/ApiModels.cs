using System;
using System.Collections.Generic;

namespace SyncLib.Core.Models.Api;

public class CredencialDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class TokenDto
{
    public string Username { get; set; } = string.Empty;
    public bool Authenticated { get; set; }
    public DateTime Created { get; set; }
    public DateTime Expiration { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public abstract class DtoBase
{
    public Guid? Id { get; set; }
}

public class ComicInfoDto : DtoBase
{
    public long? IdMal { get; set; }
    public string Comic { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;
    public float Number { get; set; }
    public float? Volume { get; set; }
    public string? Notes { get; set; }
    public int? Year { get; set; }
    public int? Month { get; set; }
    public int? Day { get; set; }
    public string? Writer { get; set; }
    public string? Penciller { get; set; }
    public string? Inker { get; set; }
    public string? CoverArtist { get; set; }
    public string? Colorist { get; set; }
    public string? Letterer { get; set; }
    public string? Publisher { get; set; }
    public string? Tags { get; set; }
    public string? Web { get; set; }
    public string? Editor { get; set; }
    public string? Translator { get; set; }
    public int? PageCount { get; set; }
    public string? Summary { get; set; }
    public string? Imprint { get; set; }
    public string? Genre { get; set; }
    public string LanguageISO { get; set; } = "pt";
    public string? Format { get; set; }
    // Enums as strings for simplicity in JSON serialization
    public string? AgeRating { get; set; } 
    public string? BlackAndWhite { get; set; }
    public string Manga { get; set; } = "Yes";
}

public class OpfDto : DtoBase
{
    public string Title { get; set; } = string.Empty;
    public string? Novel { get; set; }
    public string? Creator { get; set; }
    public string? Contributor { get; set; }
    public string? Publisher { get; set; }
    public string? DatePublished { get; set; }
    public string? Description { get; set; }
    public string? Subjects { get; set; }
    public string Language { get; set; } = "en";
    public string? Identifiers { get; set; }
    public string? Series { get; set; }
    public string? SeriesIndex { get; set; }
    public float? Volume { get; set; }
}

public class DataFileDto : DtoBase
{
    public Guid? ComicInfoId { get; set; }
    public Guid? OpfId { get; set; }
    public string? Tipo { get; set; }
    public string? FileName { get; set; }
    public string? FileContent { get; set; }
}

public class MangaDto : DtoBase
{
    public string? Nome { get; set; }
    public string? FileName { get; set; }
    public string? Extension { get; set; }
    public DateTime? FileDate { get; set; }
    public float? Volume { get; set; }
    public string? Serie { get; set; }
    public Guid? ComicInfoId { get; set; }
}

public class BookDto : DtoBase
{
    public string? Nome { get; set; }
    public string? FileName { get; set; }
    public string? Extension { get; set; }
    public DateTime? FileDate { get; set; }
    public float? Volume { get; set; }
    public string? Serie { get; set; }
    public Guid? OpfId { get; set; }
}
