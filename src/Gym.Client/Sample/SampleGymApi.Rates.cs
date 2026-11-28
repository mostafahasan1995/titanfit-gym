using Gym.Contracts;
using Gym.Domain;

namespace Gym.Client.Sample;

/// <summary>The exchange rate part of the sample: a month of rates (today's included) and payments in both currencies.</summary>
public sealed partial class SampleGymApi
{
    /// <summary>Today's sample rate: Syrian pounds for 1 US dollar.</summary>
    public const decimal SampleRate = 11_500m;

    private readonly List<ExchangeRateDto> rates = [];

    public Task<ExchangeRateStatusDto> GetExchangeRateAsync(CancellationToken ct = default) => Task.FromResult(RateStatus());

    public Task<ExchangeRateDto> SetExchangeRateAsync(SetExchangeRateRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SypPerUsd <= 0 || request.SypPerUsd > ExchangeRate.MaxSypPerUsd)
        {
            throw Bad("invalid_exchange_rate", "The exchange rate must be more than 0 pounds for 1 dollar.");
        }

        var by = current ?? staff.First(s => s.Role == StaffRole.Admin);
        var rate = new ExchangeRateDto(rates.Count == 0 ? 1 : rates.Max(r => r.Id) + 1, Today, Math.Round(request.SypPerUsd, 2), by.Id, by.FullName, DateTime.UtcNow);
        rates.Add(rate);
        Live.PublishExchangeRateChanged(RateStatus());
        return Task.FromResult(rate);
    }

    public Task<IReadOnlyList<ExchangeRateDto>> GetExchangeRatesAsync(DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default) =>
        ListOf(rates.Where(r => (from is null || r.Date >= from) && (to is null || r.Date <= to)).OrderByDescending(r => r.EnteredAt).ThenByDescending(r => r.Id));

    /// <summary>
    /// For the screenshots of the warnings: <see cref="ExchangeRateState.Stale"/> removes today's rates (yesterday's is
    /// used), <see cref="ExchangeRateState.None"/> removes them all, <see cref="ExchangeRateState.Today"/> adds today's
    /// sample rate back when it is missing. Publishes <see cref="SampleLiveConnection.ExchangeRateChanged"/>.
    /// </summary>
    public void SimulateExchangeRate(ExchangeRateState state)
    {
        switch (state)
        {
            case ExchangeRateState.None:
                rates.Clear();
                break;
            case ExchangeRateState.Stale:
                rates.RemoveAll(r => r.Date >= Today);
                if (rates.Count == 0)
                {
                    AddRate(Today.AddDays(-1), SampleRate - 100);
                }

                break;
            default:
                if (rates.All(r => r.Date < Today))
                {
                    AddRate(Today, SampleRate);
                }

                break;
        }

        Live.PublishExchangeRateChanged(RateStatus());
    }

    private ExchangeRateStatusDto RateStatus()
    {
        var newest = NewestRate();
        return new ExchangeRateStatusDto(Today, newest, CurrencyRules.GetState(newest?.Date, Today), settings.SypRoundingStep);
    }

    private ExchangeRateDto? NewestRate() => rates.OrderByDescending(r => r.EnteredAt).ThenByDescending(r => r.Id).FirstOrDefault();

    /// <summary>What the customer pays for <paramref name="usd"/>, like the server (the shown rate, else the newest).</summary>
    private PaymentAmount Due(decimal usd, PaymentRequest? payment)
    {
        decimal? rate = payment?.ExchangeRateId is { } id
            ? (rates.FirstOrDefault(r => r.Id == id) ?? throw NotFound("exchange_rate_not_found", "No such exchange rate.")).SypPerUsd
            : NewestRate()?.SypPerUsd;
        try
        {
            return CurrencyRules.Due(usd, payment?.Currency ?? Currency.Usd, rate, settings.SypRoundingStep);
        }
        catch (DomainException ex)
        {
            throw Bad(ex.Code, ex.Message);
        }
    }

    private void AddRate(DateOnly day, decimal sypPerUsd)
    {
        var admin = staff.First(s => s.Role == StaffRole.Admin);
        rates.Add(new ExchangeRateDto(rates.Count == 0 ? 1 : rates.Max(r => r.Id) + 1, day, sypPerUsd, admin.Id, admin.FullName, At(day, 8, 15)));
    }

    /// <summary>The rate of a past day (the one entered that day or before), for the seeded payments.</summary>
    private decimal? RateOn(DateOnly day) =>
        rates.Where(r => r.Date <= day).OrderByDescending(r => r.EnteredAt).Select(r => (decimal?)r.SypPerUsd).FirstOrDefault();

    /// <summary>A month of rates, one a day, slowly rising to today's <see cref="SampleRate"/>.</summary>
    private void SeedRates()
    {
        for (var back = 29; back >= 0; back--)
        {
            AddRate(Today.AddDays(-back), SampleRate - (back + 1) / 2 * 20);
        }
    }
}
