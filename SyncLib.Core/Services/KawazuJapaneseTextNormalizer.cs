using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Kawazu;

namespace SyncLib.Core.Services;

public class KawazuJapaneseTextNormalizer : IJapaneseTextNormalizer
{
    private readonly KawazuConverter _converter;

    public KawazuJapaneseTextNormalizer()
    {
        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
        string ipaDicDir = Path.Combine(baseDir, "IpaDic");
        if (Directory.Exists(ipaDicDir))
        {
            _converter = new KawazuConverter(ipaDicDir);
        }
        else
        {
            _converter = new KawazuConverter();
        }
    }

    /// <summary>
    /// Checks if string contains Hiragana, Katakana, Kanji, or Japanese punctuation.
    /// </summary>
    public bool IsJapanese(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;

        // Hiragana (3040-309F), Katakana (30A0-30FF), CJK Unified Ideographs (4E00-9FAF, 3400-4DBF), Japanese punctuation (3000-303F)
        return Regex.IsMatch(text, @"[\u3040-\u309F\u30A0-\u30FF\u4E00-\u9FAF\u3400-\u4DBF]");
    }

    public async Task<string> NormalizeToRomajiAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        string extension = Path.GetExtension(text);
        string nameWithoutExt = Path.HasExtension(text) ? Path.GetFileNameWithoutExtension(text) : text;

        // 1. Remove Author: typically separated by " - " at the end
        // e.g. " - 中村颯希 & ゆき哉" or " - 衣笠 彰梧 & トモセシュンサク" or " - 十夜 & Chibi"
        string withoutAuthor = StripAuthor(nameWithoutExt);

        // 2. Remove subtitle indicators like " — ～...～" or " — ..."
        string withoutSubtitle = Regex.Replace(withoutAuthor, @"\s*—\s*～[^～]*～\s*", " ");
        withoutSubtitle = Regex.Replace(withoutSubtitle, @"\s*—\s*~[^~]*~\s*", " ");

        // 3. Extract volume pattern if present, e.g. ", Vol. 01" or ", Vol.01" or "Vol. 06.5"
        var volMatch = Regex.Match(withoutSubtitle, @",?\s*\b[Vv]ol(?:ume)?\.?\s*(?<vol>\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
        string? volumeSuffix = null;
        string titlePart = withoutSubtitle;

        if (volMatch.Success && decimal.TryParse(volMatch.Groups["vol"].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal volNum))
        {
            volumeSuffix = $" - Volume {volNum.ToString("00.##", System.Globalization.CultureInfo.InvariantCulture)} (Jap)";
            titlePart = withoutSubtitle.Substring(0, volMatch.Index).Trim();
        }

        // 4. Clean Japanese punctuation
        titlePart = CleanJapanesePunctuation(titlePart);

        // 5. Convert Fullwidth numbers/characters to ASCII (e.g. ３年生編 -> 3年生編)
        titlePart = NormalizeFullWidth(titlePart);

        // 6. Convert Kanji/Hiragana/Katakana to Romaji using Kawazu
        string romaji = await _converter.Convert(titlePart, To.Romaji, Mode.Spaced);

        // 7. Post-process Romaji:
        // Clean double spaces, fix formatting
        romaji = PostProcessRomaji(romaji);

        // 8. Combine Title and Volume/Suffix
        string result;
        if (!string.IsNullOrEmpty(volumeSuffix))
        {
            result = $"{romaji}{volumeSuffix}";
        }
        else
        {
            result = romaji;
        }

        if (!string.IsNullOrEmpty(extension))
        {
            result += extension;
        }

        return result;
    }

    private static string StripAuthor(string input)
    {
        int lastDashIndex = input.LastIndexOf(" - ", StringComparison.Ordinal);
        if (lastDashIndex > 0)
        {
            string afterDash = input.Substring(lastDashIndex + 3).Trim();
            // Check if afterDash is not a volume (e.g. not "Volume 01" or "Vol. 01")
            if (!Regex.IsMatch(afterDash, @"^[Vv]ol(ume)?\.?\s*\d+", RegexOptions.IgnoreCase))
            {
                return input.Substring(0, lastDashIndex).Trim();
            }
        }
        return input.Trim();
    }

    private static string CleanJapanesePunctuation(string input)
    {
        return input
            .Replace("、", " ")
            .Replace("。", " ")
            .Replace("，", " ")
            .Replace("　", " ") // Fullwidth space
            .Trim();
    }

    private static string NormalizeFullWidth(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (char c in input)
        {
            // Fullwidth ASCII variants (0xFF01-0xFF5E) -> ASCII (0x21-0x7E)
            if (c >= 0xFF01 && c <= 0xFF5E)
            {
                sb.Append((char)(c - 0xFEE0));
            }
            else
            {
                sb.Append(c);
            }
        }
        return sb.ToString();
    }

    private static string PostProcessRomaji(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string text = " " + input.ToLowerInvariant() + " ";

        // Natural Japanese Hepburn particle / verb adjustments
        text = Regex.Replace(text, @"\bde\s+ha\b", "de wa");
        text = Regex.Replace(text, @"\bha\b", "wa");
        text = Regex.Replace(text, @"\bshi\s+ta\b", "shita");
        text = Regex.Replace(text, @"\bgozai\s+masu\b", "gozaimasu");
        text = Regex.Replace(text, @"\bhi\s+ta\s+kakusu\b", "hitakakusu");
        text = Regex.Replace(text, @"\bhita\s+kakusu\b", "hitakakusu");
        text = Regex.Replace(text, @"\bhi\s+ta\b", "hita");
        text = Regex.Replace(text, @"\bdai\s+seijo\b", "daiseijo");
        text = Regex.Replace(text, @"\b(\d+)\s+nensei\s+hen\b", "$1-nensei-hen");
        text = Regex.Replace(text, @"\bhe\s+(\d+-nensei-hen)\b", "$1");
        text = Regex.Replace(text, @"\btenohira\s+henshuu\b", "- henshu");
        text = Regex.Replace(text, @"\btenohira\b", "");
        text = Regex.Replace(text, @"\bhenshuu\b", "henshu");
        text = Regex.Replace(text, @"\bo\b", "wo");
        text = Regex.Replace(text, @"\bhe\b", "e");

        // Clean spaces and punctuation artifacts
        text = Regex.Replace(text, @"\s+", " ").Trim();

        // Capitalize words with proper casing for particles
        var particles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "na", "de", "wa", "ga", "no", "wo", "to", "ni", "e", "shita"
        };

        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            var w = words[i];
            if (i > 0 && particles.Contains(w))
            {
                words[i] = w.ToLowerInvariant();
            }
            else
            {
                words[i] = CapitalizeWord(w);
            }
        }

        string cleaned = string.Join(" ", words);
        cleaned = Regex.Replace(cleaned, @"\s+-\s+", " - ");
        return cleaned.Trim(' ', '-');
    }

    private static string CapitalizeWord(string word)
    {
        if (string.IsNullOrEmpty(word)) return word;

        // Keep hyphenated parts like 3-nensei-hen
        if (word.Contains('-'))
        {
            return word.ToLowerInvariant();
        }

        if (char.IsLetter(word[0]))
        {
            return char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
        }

        return word;
    }
}
