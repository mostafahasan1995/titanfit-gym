namespace Gym.Domain;

/// <summary>The currency a customer pays in. Every amount in the system is stored in US dollars.</summary>
public enum Currency
{
    /// <summary>US dollars (the system's money).</summary>
    Usd = 0,

    /// <summary>Syrian pounds, at the day's exchange rate.</summary>
    Syp = 1,
}
