using System.Threading.Tasks;

namespace SyncLib.Core.Services;

public interface IJapaneseTextNormalizer
{
    /// <summary>
    /// Checks whether the given text contains Japanese characters (Kanji, Hiragana, Katakana, etc.).
    /// </summary>
    bool IsJapanese(string? text);

    /// <summary>
    /// Normalizes a Japanese title or file name to Western Romaji format,
    /// removing authors, Japanese commas/punctuation, subtitles, adding spaces,
    /// and formatting volume indicators.
    /// </summary>
    Task<string> NormalizeToRomajiAsync(string text);
}
