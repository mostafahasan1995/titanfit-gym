using System.Text;

namespace Gym.Domain;

/// <summary>
/// Normalizes names for search, so "أحمد", "احمد" and "إحمد" match, and "فاطمة" matches "فاطمه":
/// alef forms → ا, ة → ه, ى → ي, ؤ → و, ئ → ي; diacritics and tatweel removed; Latin letters lower case;
/// Arabic-Indic digits → 0-9; runs of spaces collapsed.
/// </summary>
public static class SearchText
{
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "";
        }

        var sb = new StringBuilder(text.Length);
        var pendingSpace = false;
        foreach (var raw in text)
        {
            if (char.IsWhiteSpace(raw))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            // Tatweel, harakat (fathatan .. sukun and the extended marks) and the superscript alef.
            if (raw == 'ـ' || raw is >= 'ً' and <= 'ٟ' || raw == 'ٰ')
            {
                continue;
            }

            var c = raw switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا', // أ إ آ ٱ → ا
                'ة' => 'ه', // ة → ه
                'ى' => 'ي', // ى → ي
                'ؤ' => 'و', // ؤ → و
                'ئ' => 'ي', // ئ → ي
                _ => char.ToLowerInvariant(ToAsciiDigit(raw)),
            };

            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    /// <summary>Replaces Arabic-Indic (٠-٩) and Persian (۰-۹) digits with 0-9; everything else is kept.</summary>
    public static string NormalizeDigits(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return string.Create(text.Length, text, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = ToAsciiDigit(source[i]);
            }
        });
    }

    /// <summary>Only the digits of <paramref name="text"/> (after <see cref="NormalizeDigits"/>).</summary>
    public static string DigitsOnly(string? text) =>
        string.IsNullOrEmpty(text) ? "" : new string(NormalizeDigits(text).Where(char.IsAsciiDigit).ToArray());

    private static char ToAsciiDigit(char c) => c switch
    {
        >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
        >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
        _ => c,
    };
}
