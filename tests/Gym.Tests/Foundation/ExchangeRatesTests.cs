using Gym.Client.Sample;
using Gym.Contracts;
using Gym.Domain;
using Gym.UI;

namespace Gym.Tests.Foundation;

/// <summary>The station's exchange rate state that the pages, the pickers and the shell banner read.</summary>
public sealed class ExchangeRatesTests
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Today);

    [Fact]
    public void Before_loading_there_is_no_rate_and_dollars_are_used()
    {
        var rates = new ExchangeRates();

        Assert.Equal((false, false, ExchangeRateState.None), (rates.IsLoaded, rates.HasRate, rates.State));
        Assert.Null(rates.ToSyp(20m));
        Assert.Equal(new PaymentAmount(Currency.Usd, 20m, null), rates.Due(20m, Currency.Syp));
        Assert.Equal(new PaymentRequest(Currency.Usd, null), rates.Payment(Currency.Syp));
    }

    [Fact]
    public void Today_s_rate_converts_with_the_step_and_is_sent_with_the_payment()
    {
        var rates = new ExchangeRates();
        rates.Set(Status(Today, 11_500m, step: 1_000));

        Assert.Equal(ExchangeRateState.Today, rates.State);
        Assert.Equal(14_000m, rates.ToSyp(1.25m)); // 14,375 → 14,000
        Assert.Equal(new PaymentAmount(Currency.Syp, 230_000m, 11_500m), rates.Due(20m, Currency.Syp));
        Assert.Equal(new PaymentAmount(Currency.Usd, 20m, 11_500m), rates.Due(20m, Currency.Usd));
        Assert.Equal(new PaymentRequest(Currency.Syp, 7), rates.Payment(Currency.Syp));
    }

    [Fact]
    public void Yesterday_s_rate_is_still_used_but_stale()
    {
        var rates = new ExchangeRates();
        var changes = 0;
        rates.Changed += (_, _) => changes++;

        rates.Set(Status(Today.AddDays(-1), 11_450m));

        Assert.True(rates.IsStale);
        Assert.Equal(229_000m, rates.ToSyp(20m));
        Assert.Equal(1, changes);
    }

    [Fact]
    public async Task It_loads_from_the_api()
    {
        var api = new SampleGymApi(Today);
        var rates = new ExchangeRates();

        await rates.RefreshAsync(api);
        Assert.Equal((ExchangeRateState.Today, (decimal?)SampleGymApi.SampleRate), (rates.State, rates.SypPerUsd));

        api.SimulateExchangeRate(ExchangeRateState.None);
        await rates.RefreshAsync(api);
        Assert.Equal((true, false), (rates.IsLoaded, rates.HasRate));
    }

    private static ExchangeRateStatusDto Status(DateOnly date, decimal rate, int step = 1) =>
        new(Today, new ExchangeRateDto(7, date, rate, 1, "Admin", DateTime.UtcNow), CurrencyRules.GetState(date, Today), step);
}
