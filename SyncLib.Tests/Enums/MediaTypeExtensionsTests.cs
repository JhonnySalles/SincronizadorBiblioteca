using FluentAssertions;
using SyncLib.Core.Enums;
using Xunit;

namespace SyncLib.Tests.Enums;

public class MediaTypeExtensionsTests
{
    [Theory]
    [InlineData(MediaType.MangaPortugues, "MANGA PORTUGUÊS")]
    [InlineData(MediaType.MangaJapones, "MANGA JAPONÊS")]
    [InlineData(MediaType.MangaIngles, "MANGA INGLÊS")]
    [InlineData(MediaType.EbookPortugues, "EBOOK PORTUGUÊS")]
    [InlineData(MediaType.EbookIngles, "EBOOK INGLÊS")]
    [InlineData(MediaType.EbookJapones, "EBOOK JAPONÊS")]
    public void ToDisplayName_ShouldReturnExpectedDisplayName(MediaType mediaType, string expected)
    {
        var result = mediaType.ToDisplayName();
        result.Should().Be(expected);
    }
}
