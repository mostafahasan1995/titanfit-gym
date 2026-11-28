namespace Gym.Domain;

/// <summary>
/// One exchange rate the manager entered: Syrian pounds for 1 US dollar, for a local day. Every entry is kept (the
/// history); the newest one is the rate in use.
/// </summary>
public class ExchangeRate
{
    /// <summary>The highest rate accepted (a typing error guard for the column size, not a business limit).</summary>
    public const decimal MaxSypPerUsd = 100_000_000m;

    public int Id { get; set; }

    /// <summary>The local day the rate was entered for (the server's "today").</summary>
    public DateOnly Date { get; set; }

    /// <summary>Syrian pounds for 1 US dollar.</summary>
    public decimal SypPerUsd { get; set; }

    public int EnteredById { get; set; }

    public StaffMember? EnteredBy { get; set; }

    public DateTime EnteredAt { get; set; }

    public static ExchangeRate Create(decimal sypPerUsd, DateOnly today, int enteredById, DateTime nowUtc) => new()
    {
        Date = today,
        SypPerUsd = CurrencyRules.CheckRate(sypPerUsd),
        EnteredById = enteredById,
        EnteredAt = nowUtc,
    };
}

/// <summary>Whether a rate exists, and whether it was entered today.</summary>
public enum ExchangeRateState
{
    /// <summary>No rate was ever entered: amounts in Syrian pounds are not shown and only US dollars can be taken.</summary>
    None = 0,

    /// <summary>A rate was entered today.</summary>
    Today = 1,

    /// <summary>The last rate is from an earlier day: it is still used, with a warning.</summary>
    Stale = 2,
}
