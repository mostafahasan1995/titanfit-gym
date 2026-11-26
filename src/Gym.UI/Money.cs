using System.Globalization;

namespace Gym.UI;

/// <summary>
/// Money is whole Syrian pounds. Shown as "150,000 SYP" / "150,000 ل.س" with Western digits 0-9 in both languages
/// (docs/PLAN.md). Use these helpers (or <see cref="Controls.MoneyText"/>) instead of StringFormat in XAML, which
/// would follow the Arabic culture's separators.
/// </summary>
public static class Money
{
    // Keeps the number (and its minus sign) left-to-right inside Arabic text.
    private const char LeftToRightEmbedding = '‪';
    private const char PopDirectionalFormatting = '‬';

    /// <summary>"150,000 SYP" or "150,000 ل.س".</summary>
    public static string Format(long amount, AppLanguage language) => language == AppLanguage.Arabic
        ? $"{LeftToRightEmbedding}{NumberText.Format(amount)}{PopDirectionalFormatting} {CommonText.Currency.Ar}"
        : $"{NumberText.Format(amount)} {CommonText.Currency.En}";

    /// <summary>"150,000" without the currency.</summary>
    public static string FormatAmount(long amount) => NumberText.Format(amount);

    /// <summary>Reads what the user typed ("150000", "150,000", "١٥٠٬٠٠٠"). Null when it is not a whole amount.</summary>
    public static long? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var digits = Gym.Domain.SearchText.NormalizeDigits(text.Trim())
            .Replace(",", "", StringComparison.Ordinal)
            .Replace("٬", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);
        return long.TryParse(digits, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount) ? amount : null;
    }
}

/// <summary>Whole numbers with thousands separators and Western digits: "1,250".</summary>
public static class NumberText
{
    public static string Format(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
