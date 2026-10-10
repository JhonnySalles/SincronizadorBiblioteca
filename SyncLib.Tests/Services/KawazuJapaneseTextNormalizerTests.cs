using System.Threading.Tasks;
using FluentAssertions;
using Kawazu;
using SyncLib.Core.Services;
using Xunit;

namespace SyncLib.Tests.Services;

public class KawazuJapaneseTextNormalizerTests
{
    private readonly KawazuJapaneseTextNormalizer _normalizer = new();



    [Theory]
    [InlineData("ふつつかな悪女ではございますが", true)]
    [InlineData("ようこそ実力至上主義の教室へ", true)]
    [InlineData("転生した大聖女は、聖女であることをひた隠す", true)]
    [InlineData("Solo Leveling Vol 01", false)]
    [InlineData("Harry Potter and the Philosopher's Stone", false)]
    public void IsJapanese_ShouldDetectJapaneseCharactersCorrectly(string text, bool expected)
    {
        var result = _normalizer.IsJapanese(text);
        result.Should().Be(expected);
    }

    [Fact]
    public async Task NormalizeToRomajiAsync_ShouldNormalizeExample1_Futsutsuka()
    {
        string input = "ふつつかな悪女ではございますが 掌編集, Vol. 01 — ～雛宮蝶鼠とりかえ伝～ - 中村颯希 & ゆき哉.epub";
        string result = await _normalizer.NormalizeToRomajiAsync(input);

        // Expected format contains romaji title, volume 01, (Jap) and .epub
        result.Should().Be("Futsutsuka na Akujo de wa Gozaimasu ga - Henshu - Volume 01 (Jap).epub");
    }

    [Fact]
    public async Task NormalizeToRomajiAsync_ShouldNormalizeExample2_Youkoso()
    {
        string input = "ようこそ実力至上主義の教室へ ３年生編, Vol. 04 - 衣笠 彰梧 & トモセシュンサク.epub";
        string result = await _normalizer.NormalizeToRomajiAsync(input);

        result.Should().Be("Youkoso Jitsuryoku Shijou Shugi no Kyoushitsu 3-nensei-hen - Volume 04 (Jap).epub");
    }

    [Fact]
    public async Task NormalizeToRomajiAsync_ShouldNormalizeExample3_Tensei()
    {
        string input = "転生した大聖女は、聖女であることをひた隠す, Vol. 03 - 十夜 & Chibi.epub";
        string result = await _normalizer.NormalizeToRomajiAsync(input);

        result.Should().Be("Tensei shita Daiseijo wa Seijo de Aru Koto wo Hitakakusu - Volume 03 (Jap).epub");
    }

    [Fact]
    public async Task NormalizeToRomajiAsync_ShouldNormalizeDecimalVolume()
    {
        string input = "ようこそ実力至上主義の教室へ, Vol. 04.5 - 衣笠 彰梧 & トモセシュンサク.epub";
        string result = await _normalizer.NormalizeToRomajiAsync(input);

        result.Should().Be("Youkoso Jitsuryoku Shijou Shugi no Kyoushitsu e - Volume 04.5 (Jap).epub");
    }
}
