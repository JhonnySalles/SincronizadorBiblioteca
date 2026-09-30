using FluentAssertions;
using SyncLib.Core.Enums;
using SyncLib.Core.Helpers;
using Xunit;

namespace SyncLib.Tests.Helpers;

public class ExtensionHelperTests
{
    #region ToFileExtension (MangaExtension) Tests

    [Theory]
    [InlineData(MangaExtension.Cbz, ".cbz")]
    [InlineData(MangaExtension.Cbr, ".cbr")]
    [InlineData(MangaExtension.Cb7, ".cb7")]
    [InlineData(MangaExtension.Cbt, ".cbt")]
    [InlineData(MangaExtension.Zip, ".zip")]
    [InlineData(MangaExtension.Rar, ".rar")]
    [InlineData(MangaExtension.SevenZip, ".7z")]
    [InlineData(MangaExtension.Tar, ".tar")]
    public void ToFileExtension_MangaExtension_ShouldReturnCorrectExtension(MangaExtension ext, string expected)
    {
        var result = ExtensionHelper.ToFileExtension(ext);
        result.Should().Be(expected);
    }

    #endregion

    #region ToFileExtension (EbookExtension) Tests

    [Theory]
    [InlineData(EbookExtension.Epub, ".epub")]
    [InlineData(EbookExtension.Epub3, ".epub3")]
    [InlineData(EbookExtension.Pdf, ".pdf")]
    [InlineData(EbookExtension.Mobi, ".mobi")]
    [InlineData(EbookExtension.Djvu, ".djvu")]
    [InlineData(EbookExtension.Fb2, ".fb2")]
    [InlineData(EbookExtension.Azw, ".azw")]
    [InlineData(EbookExtension.Azw3, ".azw3")]
    [InlineData(EbookExtension.Doc, ".doc")]
    [InlineData(EbookExtension.Tiff, ".tiff")]
    [InlineData(EbookExtension.Odt, ".odt")]
    [InlineData(EbookExtension.Opds, ".opds")]
    public void ToFileExtension_EbookExtension_ShouldReturnCorrectExtension(EbookExtension ext, string expected)
    {
        var result = ExtensionHelper.ToFileExtension(ext);
        result.Should().Be(expected);
    }

    #endregion

    #region GetSupportedExtensions Tests

    [Theory]
    [InlineData(MediaType.MangaPortugues)]
    [InlineData(MediaType.MangaJapones)]
    [InlineData(MediaType.MangaIngles)]
    [InlineData(MediaType.EbookPortugues)]
    [InlineData(MediaType.EbookIngles)]
    [InlineData(MediaType.EbookJapones)]
    public void GetSupportedExtensions_ShouldReturnNonEmptySet(MediaType mediaType)
    {
        var extensions = ExtensionHelper.GetSupportedExtensions(mediaType);
        
        extensions.Should().NotBeNull();
        extensions.Should().NotBeEmpty();
    }

    [Fact]
    public void GetSupportedExtensions_Manga_ShouldContainMangaFormats()
    {
        var extensions = ExtensionHelper.GetSupportedExtensions(MediaType.MangaPortugues);

        extensions.Should().Contain(".cbz");
        extensions.Should().Contain(".cbr");
        extensions.Should().Contain(".zip");
        extensions.Should().Contain(".7z");
        extensions.Should().NotContain(".epub");
    }

    [Fact]
    public void GetSupportedExtensions_Ebook_ShouldContainEbookFormats()
    {
        var extensions = ExtensionHelper.GetSupportedExtensions(MediaType.EbookPortugues);

        extensions.Should().Contain(".epub");
        extensions.Should().Contain(".pdf");
        extensions.Should().Contain(".mobi");
        extensions.Should().Contain(".azw3");
        extensions.Should().NotContain(".cbz");
    }

    #endregion

    #region IsSupportedFile Tests

    [Theory]
    [InlineData("manga.cbz", MediaType.MangaPortugues, true)]
    [InlineData("MANGA.CBZ", MediaType.MangaIngles, true)]
    [InlineData("manga.7z", MediaType.MangaJapones, true)]
    [InlineData("book.epub", MediaType.EbookPortugues, true)]
    [InlineData("BOOK.EPUB", MediaType.EbookIngles, true)]
    [InlineData("doc.pdf", MediaType.EbookJapones, true)]
    [InlineData("video.mp4", MediaType.MangaPortugues, false)]
    [InlineData("audio.mp3", MediaType.EbookPortugues, false)]
    [InlineData("script.exe", MediaType.MangaIngles, false)]
    public void IsSupportedFile_ShouldValidateCorrectly(string fileName, MediaType mediaType, bool expected)
    {
        var result = ExtensionHelper.IsSupportedFile(fileName, mediaType);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, MediaType.MangaPortugues)]
    [InlineData("", MediaType.MangaPortugues)]
    [InlineData("   ", MediaType.MangaPortugues)]
    [InlineData("file_without_extension", MediaType.MangaPortugues)]
    public void IsSupportedFile_ShouldReturnFalse_WhenFileIsInvalidOrWithoutExtension(string? fileName, MediaType mediaType)
    {
        var result = ExtensionHelper.IsSupportedFile(fileName!, mediaType);
        result.Should().BeFalse();
    }

    #endregion
}
