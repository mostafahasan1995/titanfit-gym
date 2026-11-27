using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace Gym.UI;

/// <summary>
/// Data text (names, station names, codes) in the Arabic layout: text without Arabic letters is shown left-to-right,
/// so "Reception 1" does not turn into "1 Reception". Give the TextBlock <c>HorizontalAlignment="Left"</c> (the start side).
/// <para>
/// Numbers inside an Arabic sentence: a number in groups ("0944 123 456") or several numbers in a row
/// ("#00412 · 0944 123 456") come out in reverse order in right-to-left text, because the spaces between the groups
/// take the sentence direction. <see cref="FixNumbers"/> and <see cref="Ltr"/> keep them left-to-right and in their place.
/// </para>
/// </summary>
public static partial class BidiText
{
    /// <summary>Left-to-right mark: starts a left-to-right run.</summary>
    public const char Lrm = '\u200E';

    /// <summary>Right-to-left mark: ends the run, so the text after it follows the Arabic sentence again.</summary>
    public const char Rlm = '\u200F';

    /// <summary>True when the text has Arabic (right-to-left) letters.</summary>
    public static bool HasRtl(string? text) => text is not null && text.Any(c => c is >= '\u0590' and <= '\u08FF' or >= '\uFB1D' and <= '\uFEFC');

    /// <summary>Sets the text and its direction: its own when it has Arabic letters, otherwise left-to-right.</summary>
    public static void SetText(TextBlock block, string? text)
    {
        ArgumentNullException.ThrowIfNull(block);
        block.Text = text ?? "";
        if (HasRtl(text))
        {
            block.ClearValue(FrameworkElement.FlowDirectionProperty);
        }
        else
        {
            block.FlowDirection = FlowDirection.LeftToRight;
        }
    }

    /// <summary>
    /// One left-to-right value (phone, member number, code) to put inside an Arabic sentence. WPF resolves digits
    /// after Arabic letters as Arabic numbers, so the spaces and signs between them take the sentence direction and the
    /// groups come out reversed; WPF also has no Unicode isolates (U+2066 to U+2069) and ignores the embedding controls.
    /// So every character that is not a letter or digit gets a left-to-right mark on both sides (that keeps the whole
    /// value one left-to-right run, like <see cref="Money"/> does for the minus sign), and a right-to-left mark closes it
    /// (the text after it follows the Arabic sentence again).
    /// </summary>
    public static string Ltr(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        var builder = new StringBuilder(value.Length * 3 + 2);
        builder.Append(Lrm);
        foreach (var c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else
            {
                builder.Append(Lrm).Append(c).Append(Lrm);
            }
        }

        return builder.Append(Rlm).ToString();
    }

    /// <summary>
    /// Arabic text: wraps every number run with <see cref="Ltr"/>, so "مثل 0944 123 456." reads 0944 123 456 and not
    /// 456 123 0944. A run is digits 0-9 with single spaces, dashes, dots, slashes, commas or colons between the
    /// groups, and an optional leading + or # ("+963 944 123 456", "#00412", "1,400,000", "18:42"). Runs that touch a
    /// letter (codes such as "TF1-…") are left alone. English text is returned unchanged.
    /// </summary>
    public static string FixNumbers(string? text, AppLanguage language)
    {
        if (string.IsNullOrEmpty(text) || language != AppLanguage.Arabic)
        {
            return text ?? "";
        }

        return NumberRun().Replace(text, match => Ltr(match.Value));
    }

    // Not after a letter, a digit or a mark (already wrapped), nor inside a run ("2,000" from its "000"); atomic, so a
    // run that touches a letter or a mark is skipped as a whole instead of being cut shorter.
    [GeneratedRegex(@"(?<![\p{L}\p{Nd}\u200E])(?<![0-9][ \-/.:,])(?>[+#]?[0-9]+(?:[ \-/.:,][0-9]+)*)(?![\p{L}\p{Nd}\u200F])")]
    private static partial Regex NumberRun();
}
