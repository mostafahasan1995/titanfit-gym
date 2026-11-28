using System.Globalization;
using Gym.Domain;

namespace Gym.UI;

/// <summary>
/// Money is stored in US dollars (2 decimals): "$12.50" in both languages. Amounts in Syrian pounds (the equivalent
/// at the day's rate, or what a customer paid in pounds) are whole pounds: "150,000 SYP" / "150,000 ل.س". Western
/// digits 0-9 in both languages (docs/PLAN.md). Use these helpers (or <see cref="Controls.MoneyText"/> and
/// <see cref="Controls.SypText"/>) instead of StringFormat in XAML, which would follow the Arabic culture's separators.
/// </summary>
public static class Money
{
    /// <summary>The dollar sign, written before the amount.</summary>
    public const string UsdSign = "$";

    // Left-to-right marks around the minus sign keep "-2,000" in that order inside Arabic text
    // (WPF does not apply the bidi embedding characters).
    private const string ArabicMinus = "‎-‎";

    /// <summary>
    /// US dollars: "$12.50" ("-$2.00"). In Arabic the sign and the number are kept left-to-right with marks and a
    /// right-to-left mark closes the amount, so the text after it follows the Arabic sentence again.
    /// </summary>
    public static string Format(decimal usd, AppLanguage language)
    {
        var sign = usd < 0 ? "-" : "";
        var number = FormatAmount(Math.Abs(usd));
        return language == AppLanguage.Arabic
            ? $"{BidiText.Lrm}{sign}{UsdSign}{BidiText.Lrm}{number}{BidiText.Rlm}"
            : $"{sign}{UsdSign}{number}";
    }

    /// <summary>"12.50" without the dollar sign (always 2 decimals).</summary>
    public static string FormatAmount(decimal usd) => CurrencyRules.RoundUsd(usd).ToString("N2", CultureInfo.InvariantCulture);

    /// <summary>Syrian pounds, whole: "150,000 SYP" or "150,000 ل.س".</summary>
    public static string FormatSyp(decimal syp, AppLanguage language)
    {
        var whole = Math.Round(syp, 0, MidpointRounding.AwayFromZero);
        return language == AppLanguage.Arabic
            ? $"{(whole < 0 ? ArabicMinus + FormatSypAmount(-whole) : FormatSypAmount(whole))} {CommonText.Currency.Ar}"
            : $"{FormatSypAmount(whole)} {CommonText.Currency.En}";
    }

    /// <summary>"150,000" without the currency.</summary>
    public static string FormatSypAmount(decimal syp) => Math.Round(syp, 0, MidpointRounding.AwayFromZero).ToString("N0", CultureInfo.InvariantCulture);

    /// <summary>An amount in the given currency (dollars with cents, or whole pounds).</summary>
    public static string Format(Currency currency, decimal amount, AppLanguage language) =>
        currency == Currency.Syp ? FormatSyp(amount, language) : Format(amount, language);

    /// <summary>The amount without its currency, in the given currency's form.</summary>
    public static string FormatAmount(Currency currency, decimal amount) =>
        currency == Currency.Syp ? FormatSypAmount(amount) : FormatAmount(amount);

    /// <summary>
    /// Dollars and, when a rate is known, the pounds at that rate: "$20.00 · 230,000 SYP". <paramref name="syp"/> null
    /// gives only the dollars.
    /// </summary>
    public static string FormatWithSyp(decimal usd, decimal? syp, AppLanguage language) =>
        syp is { } pounds ? $"{Format(usd, language)} · {FormatSyp(pounds, language)}" : Format(usd, language);

    /// <summary>
    /// Reads a dollar amount the user typed ("12.5", "1,250.00", "١٢٫٥", "$12"). Null when it is not a number or has
    /// more than 2 decimals.
    /// </summary>
    public static decimal? Parse(string? text)
    {
        var cleaned = Clean(text, allowDecimals: true);
        if (cleaned is null
            || !decimal.TryParse(cleaned, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var amount))
        {
            return null;
        }

        return decimal.Round(amount, CurrencyRules.UsdDecimals) == amount ? amount : null;
    }

    /// <summary>Reads whole Syrian pounds ("150000", "150,000", "١٥٠٬٠٠٠"). Null when it is not a whole amount.</summary>
    public static decimal? ParseSyp(string? text)
    {
        var cleaned = Clean(text, allowDecimals: false);
        return cleaned is not null && long.TryParse(cleaned, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var amount) ? amount : null;
    }

    /// <summary>Reads an amount in the given currency (<see cref="Parse"/> or <see cref="ParseSyp"/>).</summary>
    public static decimal? Parse(Currency currency, string? text) => currency == Currency.Syp ? ParseSyp(text) : Parse(text);

    private static string? Clean(string? text, bool allowDecimals)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var cleaned = Gym.Domain.SearchText.NormalizeDigits(text.Trim())
            .Replace(UsdSign, "", StringComparison.Ordinal)
            .Replace(",", "", StringComparison.Ordinal)
            .Replace("٬", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("٫", ".", StringComparison.Ordinal);
        return allowDecimals || !cleaned.Contains('.', StringComparison.Ordinal) ? cleaned : null;
    }
}

/// <summary>Whole numbers with thousands separators and Western digits: "1,250".</summary>
public static class NumberText
{
    public static string Format(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
