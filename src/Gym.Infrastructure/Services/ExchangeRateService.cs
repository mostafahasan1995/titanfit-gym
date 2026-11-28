namespace Gym.Infrastructure.Services;

/// <summary>
/// The daily exchange rate (Syrian pounds for 1 US dollar), entered by hand by the manager. Every entry is kept; the
/// newest one is used, also on a later day (with a warning on the stations).
/// </summary>
public sealed class ExchangeRateService(GymDbContext db, GymClock clock, SettingsService settings)
{
    public const int MaxHistory = 1000;

    /// <summary>The newest rate and its state for today.</summary>
    public async Task<ExchangeRateStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var rate = await Project(Newest(db.ExchangeRates.AsNoTracking())).FirstOrDefaultAsync(ct);
        var step = (await settings.GetAsync(ct)).SypRoundingStep;
        var today = clock.Today;
        return new ExchangeRateStatusDto(today, rate, CurrencyRules.GetState(rate?.Date, today), step);
    }

    /// <summary>Saves today's rate (a second entry on the same day replaces it as the one in use; both stay in the history).</summary>
    public async Task<ExchangeRateDto> SetAsync(SetExchangeRateRequest request, int staffId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rate = ExchangeRate.Create(request.SypPerUsd, clock.Today, staffId, clock.UtcNow);
        db.ExchangeRates.Add(rate);
        await db.SaveChangesAsync(ct);
        return await Project(db.ExchangeRates.AsNoTracking().Where(r => r.Id == rate.Id)).SingleAsync(ct);
    }

    /// <summary>The history, newest first (local days, both ends included; no range = everything).</summary>
    public async Task<IReadOnlyList<ExchangeRateDto>> ListAsync(DateOnly? from, DateOnly? to, CancellationToken ct = default)
    {
        var query = db.ExchangeRates.AsNoTracking();
        if (from is { } start)
        {
            query = query.Where(r => r.Date >= start);
        }

        if (to is { } end)
        {
            query = query.Where(r => r.Date <= end);
        }

        return await Project(Newest(query)).Take(MaxHistory).ToListAsync(ct);
    }

    /// <summary>
    /// The amount the customer pays for <paramref name="usd"/>. The rate is the one the station showed
    /// (<see cref="PaymentRequest.ExchangeRateId"/>) or else the newest; pounds need a rate (<c>no_exchange_rate</c>).
    /// </summary>
    public async Task<PaymentAmount> GetDueAsync(decimal usd, PaymentRequest? payment, CancellationToken ct = default)
    {
        var currency = payment?.Currency ?? Currency.Usd;
        decimal? rate;
        if (payment?.ExchangeRateId is { } id)
        {
            rate = await db.ExchangeRates.AsNoTracking().Where(r => r.Id == id).Select(r => (decimal?)r.SypPerUsd).FirstOrDefaultAsync(ct)
                ?? throw new NotFoundException("exchange_rate_not_found", $"Exchange rate {id} does not exist.");
        }
        else
        {
            rate = await Newest(db.ExchangeRates.AsNoTracking()).Select(r => (decimal?)r.SypPerUsd).FirstOrDefaultAsync(ct);
        }

        var step = (await settings.GetAsync(ct)).SypRoundingStep;
        return CurrencyRules.Due(usd, currency, rate, step);
    }

    private static IQueryable<ExchangeRate> Newest(IQueryable<ExchangeRate> query) =>
        query.OrderByDescending(r => r.EnteredAt).ThenByDescending(r => r.Id);

    private static IQueryable<ExchangeRateDto> Project(IQueryable<ExchangeRate> query) =>
        query.Select(r => new ExchangeRateDto(r.Id, r.Date, r.SypPerUsd, r.EnteredById, r.EnteredBy!.FullName, r.EnteredAt));
}
