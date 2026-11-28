namespace Gym.Domain;

/// <summary>
/// Money rules: amounts are US dollars with 2 decimals; an amount in Syrian pounds is the dollar amount × the rate
/// (pounds per dollar), rounded to the nearest whole multiple of the rounding step (default 1 pound; a half goes up).
/// </summary>
public static class CurrencyRules
{
    public const int UsdDecimals = 2;
    public const int DefaultSypRoundingStep = 1;
    public const int MaxSypRoundingStep = 100_000;

    /// <summary>The largest amount in dollars a price or payment can have (fits the database column).</summary>
    public const decimal MaxUsd = 1_000_000_000m;

    /// <summary>Rounds to cents (a half cent goes up).</summary>
    public static decimal RoundUsd(decimal usd) => Math.Round(usd, UsdDecimals, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The amount in Syrian pounds for <paramref name="usd"/> at <paramref name="sypPerUsd"/>, rounded to the nearest
    /// multiple of <paramref name="roundingStep"/> (a half goes up; a step below 1 counts as 1).
    /// </summary>
    public static decimal ToSyp(decimal usd, decimal sypPerUsd, int roundingStep = DefaultSypRoundingStep)
    {
        var step = Math.Max(roundingStep, 1);
        return Math.Round(usd * sypPerUsd / step, 0, MidpointRounding.AwayFromZero) * step;
    }

    /// <summary>The dollar value of an amount in Syrian pounds (rounded to cents). Zero rate: zero.</summary>
    public static decimal ToUsd(decimal syp, decimal sypPerUsd) => sypPerUsd <= 0 ? 0m : RoundUsd(syp / sypPerUsd);

    /// <summary>The state of the newest rate (from <paramref name="lastRateDate"/>) on <paramref name="today"/>.</summary>
    public static ExchangeRateState GetState(DateOnly? lastRateDate, DateOnly today) => lastRateDate switch
    {
        null => ExchangeRateState.None,
        { } date when date >= today => ExchangeRateState.Today,
        _ => ExchangeRateState.Stale,
    };

    /// <summary>
    /// What the customer pays for <paramref name="usd"/>: in dollars the same amount, in pounds the converted and
    /// rounded amount. Paying in pounds needs a rate (<c>no_exchange_rate</c>); paying in dollars does not.
    /// </summary>
    public static PaymentAmount Due(decimal usd, Currency currency, decimal? sypPerUsd, int roundingStep = DefaultSypRoundingStep)
    {
        if (!Enum.IsDefined(currency))
        {
            throw new DomainException("validation", "The currency is not valid.");
        }

        if (currency == Currency.Usd)
        {
            return new PaymentAmount(Currency.Usd, usd, sypPerUsd);
        }

        if (sypPerUsd is not { } rate || rate <= 0)
        {
            throw new DomainException("no_exchange_rate", "No exchange rate has been entered yet: take the payment in US dollars.");
        }

        return new PaymentAmount(Currency.Syp, ToSyp(usd, rate, roundingStep), rate);
    }

    /// <summary>A rate must be above 0 (pounds for 1 dollar).</summary>
    public static decimal CheckRate(decimal sypPerUsd)
    {
        if (sypPerUsd <= 0 || sypPerUsd > ExchangeRate.MaxSypPerUsd)
        {
            throw new DomainException("invalid_exchange_rate", "The exchange rate must be more than 0 pounds for 1 dollar.");
        }

        return Math.Round(sypPerUsd, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>A dollar amount (price, fee, cash): not negative, at most 2 decimals are kept.</summary>
    public static decimal CheckUsd(decimal usd, string field)
    {
        if (usd < 0)
        {
            throw new DomainException("validation", $"{field} cannot be negative.");
        }

        if (usd > MaxUsd)
        {
            throw new DomainException("validation", $"{field} is too large.");
        }

        return RoundUsd(usd);
    }
}

/// <summary>
/// The amount a customer pays: <see cref="Amount"/> in <see cref="Currency"/> (whole pounds after rounding, or
/// dollars), and the rate used (pounds per dollar; also kept for a dollar payment when one exists).
/// </summary>
public sealed record PaymentAmount(Currency Currency, decimal Amount, decimal? SypPerUsd);
