using Gym.Domain;
using Gym.UI;

namespace Gym.Reception.Logic;

/// <summary>
/// What reception asks the customer to pay. Prices are US dollars; in Syrian pounds each payment is converted and
/// rounded on its own (the same as the server stores it), then added up. Without a rate it stays in dollars.
/// </summary>
internal static class PaymentDue
{
    /// <summary>The amount due for one or more payments (e.g. the membership and the private training service).</summary>
    public static PaymentAmount For(IReadOnlyList<decimal> usd, Currency currency, decimal? sypPerUsd, int roundingStep)
    {
        ArgumentNullException.ThrowIfNull(usd);
        if (currency == Currency.Syp && sypPerUsd is { } rate && rate > 0)
        {
            return new PaymentAmount(Currency.Syp, usd.Sum(u => CurrencyRules.ToSyp(u, rate, roundingStep)), rate);
        }

        return new PaymentAmount(Currency.Usd, usd.Sum(), sypPerUsd);
    }

    /// <summary>The amount due at the station's current rate.</summary>
    public static PaymentAmount Now(IReadOnlyList<decimal> usd, Currency currency) =>
        For(usd, currency, ExchangeRates.Current.SypPerUsd, ExchangeRates.Current.RoundingStep);

    /// <summary>
    /// The line under the amount due: the dollars when paying in pounds ("That is $25.00"), the pounds at the rate when
    /// paying in dollars ("That is 287,500 SYP"), empty in dollars without a rate. Numbers kept in order in Arabic.
    /// </summary>
    public static string OtherLine(decimal usd, PaymentAmount due, int roundingStep, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(due);
        string? other = due.Currency == Currency.Syp
            ? Money.Format(usd, language)
            : due.SypPerUsd is { } rate && rate > 0 ? Money.FormatSyp(CurrencyRules.ToSyp(usd, rate, roundingStep), language) : null;
        return other is null ? "" : BidiText.FixNumbers(ReceptionText.EqualsAmount.Format(language, other), language);
    }

    /// <summary>A payment in the history: empty for dollars; "230,000 SYP" for pounds.</summary>
    public static string PaidLine(Currency currency, decimal paidAmount, AppLanguage language) =>
        currency == Currency.Syp ? Money.FormatSyp(paidAmount, language) : "";

    /// <summary>The tooltip of a payment in pounds: "Paid 230,000 SYP at $1 = 11,500 SYP"; null for dollars.</summary>
    public static string? PaidDetail(Currency currency, decimal paidAmount, decimal? rate, AppLanguage language) =>
        currency == Currency.Syp && rate is { } r
            ? BidiText.FixNumbers(CommonText.PaidInSypAtRate.Format(language, Money.FormatSyp(paidAmount, language), Money.FormatSyp(r, language)), language)
            : null;
}
