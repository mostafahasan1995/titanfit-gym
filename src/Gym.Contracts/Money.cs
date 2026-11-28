namespace Gym.Contracts;

/// <summary>
/// How the customer pays. <see cref="ExchangeRateId"/> is the rate the station showed (so the amount in pounds is the
/// one the customer saw); without it the server uses the newest rate. Paying in pounds needs a rate
/// (<c>no_exchange_rate</c>).
/// </summary>
public sealed record PaymentRequest(Currency Currency = Currency.Usd, int? ExchangeRateId = null);

/// <summary>One exchange rate the manager entered: Syrian pounds for 1 US dollar.</summary>
public sealed record ExchangeRateDto(int Id, DateOnly Date, decimal SypPerUsd, int EnteredById, string EnteredBy, DateTime EnteredAt);

/// <summary>
/// The rate in use: the newest one entered (<see cref="Rate"/>, null when none exists yet). <see cref="State"/> says
/// whether it is today's (<see cref="ExchangeRateState.Today"/>), an older one still in use
/// (<see cref="ExchangeRateState.Stale"/>, warn) or missing (<see cref="ExchangeRateState.None"/>: amounts in pounds are
/// hidden, dollars still work). <see cref="Today"/> is the server's local day.
/// </summary>
public sealed record ExchangeRateStatusDto(DateOnly Today, ExchangeRateDto? Rate, ExchangeRateState State, int SypRoundingStep);

/// <summary>Today's rate: Syrian pounds for 1 US dollar (more than 0).</summary>
public sealed record SetExchangeRateRequest(decimal SypPerUsd);

/// <summary>
/// What was taken in each currency: <see cref="Usd"/> = the payments made in dollars, <see cref="Syp"/> = the pounds
/// taken (whole pounds) and <see cref="SypAsUsd"/> = their dollar value. Totals elsewhere are in dollars.
/// </summary>
public sealed record CurrencyTotals(decimal Usd, decimal Syp, decimal SypAsUsd, int UsdCount = 0, int SypCount = 0);
