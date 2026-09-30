using FluentAssertions;
using SyncLib.Core.Entities;
using SyncLib.Core.Enums;
using SyncLib.Core.Helpers;
using Xunit;

namespace SyncLib.Tests.Helpers;

public class FileNameProcessorTests
{
    #region CleanSeriesName Tests

    [Theory]
    [InlineData("One Piece", "One Piece")]
    [InlineData("Naruto_Shippuden", "Naruto - Shippuden")]
    [InlineData("Berserk 'Deluxe'", "Berserk Deluxe")]
    [InlineData("Dragon \"Ball\"", "Dragon Ball")]
    [InlineData("  Bleach   ", "Bleach")]
    [InlineData("Jujutsu  Kaisen", "Jujutsu Kaisen")]
    public void CleanSeriesName_ShouldNormalizeSpacesAndQuotes(string input, string expected)
    {
        var result = FileNameProcessor.CleanSeriesName(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void CleanSeriesName_ShouldReturnEmpty_WhenInputIsNullOrEmpty(string? input, string expected)
    {
        var result = FileNameProcessor.CleanSeriesName(input!);
        result.Should().Be(expected);
    }

    #endregion

    #region ExtractSeriesAndVolumeFromFinalFileName Tests

    [Theory]
    [InlineData("One Piece - Vol. 01.cbz", "One Piece", 1)]
    [InlineData("Naruto - Vol 05.cbz", "Naruto", 5)]
    [InlineData("Bleach - Volume 10.zip", "Bleach", 10)]
    [InlineData("Chainsaw Man - Vol. 12.epub", "Chainsaw Man", 12)]
    public void ExtractSeriesAndVolumeFromFinalFileName_ShouldExtractCorrectSeriesAndVolume(string fileName, string expectedSeries, int expectedVolume)
    {
        var (series, volume) = FileNameProcessor.ExtractSeriesAndVolumeFromFinalFileName(fileName);
        
        series.Should().Be(expectedSeries);
        volume.Should().Be(expectedVolume);
    }

    [Theory]
    [InlineData("One Piece.cbz", "One Piece", null)]
    [InlineData("Berserk - 03.cbz", "Berserk - 03", null)]
    [InlineData("Artbook Special Edition.pdf", "Artbook Special Edition", null)]
    [InlineData("Solo Leveling Side Story.zip", "Solo Leveling Side Story", null)]
    public void ExtractSeriesAndVolumeFromFinalFileName_ShouldReturnNullVolume_WhenNoVolumeInName(string fileName, string expectedSeries, int? expectedVolume)
    {
        var (series, volume) = FileNameProcessor.ExtractSeriesAndVolumeFromFinalFileName(fileName);
        
        series.Should().Be(expectedSeries);
        volume.Should().Be(expectedVolume);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ExtractSeriesAndVolumeFromFinalFileName_ShouldHandleNullOrEmpty(string? fileName)
    {
        var (series, volume) = FileNameProcessor.ExtractSeriesAndVolumeFromFinalFileName(fileName!);
        
        series.Should().BeEmpty();
        volume.Should().BeNull();
    }

    #endregion

    #region ApplyTemplate Tests

    [Fact]
    public void ApplyTemplate_ShouldApplyVolumeAndLangSuffix_WhenVolumeExists()
    {
        var template = "One Piece - Vol. {Volume:D2}{LangSuffix}{Extension}";
        var result = FileNameProcessor.ApplyTemplate(template, 5, " (PT-BR)", ".cbz");

        result.Should().Be("One Piece - Vol. 05 (PT-BR).cbz");
    }

    [Fact]
    public void ApplyTemplate_ShouldHandleNullVolumeGracefully()
    {
        var template = "One Piece - Vol. {Volume:D2}{LangSuffix}{Extension}";
        var result = FileNameProcessor.ApplyTemplate(template, null, "", ".cbz");

        result.Should().Be("One Piece - Vol. {Volume:D2}.cbz");
    }

    [Fact]
    public void ApplyTemplate_ShouldIncludeExtensionCorrectly()
    {
        var template = "Capitulo {Volume}{Extension}";
        var result = FileNameProcessor.ApplyTemplate(template, 100, "", ".pdf");

        result.Should().Be("Capitulo 100.pdf");
    }

    #endregion

    #region Process Tests

    [Fact]
    public void Process_ShouldFormatWithMatchingPattern_WhenPatternExists()
    {
        var patterns = new List<NamingPattern>
        {
            new NamingPattern
            {
                OriginalRawSeries = "One Piece",
                CustomTemplate = "One Piece - Vol. {Volume:D2}{LangSuffix}{Extension}"
            }
        };

        var result = FileNameProcessor.Process(
            originalFileName: "One Piece - Vol. 01.cbz",
            mediaType: MediaType.MangaPortugues,
            savedPatterns: patterns,
            customSuffix: "[PT-BR]"
        );

        result.FormattedFileName.Should().Be("One Piece - Vol. 01 [PT-BR].cbz");
        result.SeriesName.Should().Be("One Piece");
        result.VolumeNumber.Should().Be(1);
    }

    [Fact]
    public void Process_ShouldFallbackToStandardFormat_WhenNoPatternMatches()
    {
        var result = FileNameProcessor.Process(
            originalFileName: "One Piece - Vol. 05.cbz",
            mediaType: MediaType.MangaPortugues,
            savedPatterns: null,
            customSuffix: null
        );

        result.FormattedFileName.Should().Be("One Piece - Volume 05.cbz");
        result.SeriesName.Should().Be("One Piece");
        result.VolumeNumber.Should().Be(5);
    }

    #endregion

    #region Normalize Tests

    [Theory]
    [InlineData("One Piece", "onepiece")]
    [InlineData("Naruto_Shippuden", "narutoshippuden")]
    [InlineData("Berserk - 'Deluxe'", "berserkdeluxe")]
    public void Normalize_ShouldRemoveSpecialCharsAndPunctuation(string input, string expected)
    {
        var result = FileNameProcessor.Normalize(input);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    public void Normalize_ShouldReturnEmpty_WhenInputIsNullOrEmpty(string? input, string expected)
    {
        var result = FileNameProcessor.Normalize(input!);
        result.Should().Be(expected);
    }

    #endregion
}
