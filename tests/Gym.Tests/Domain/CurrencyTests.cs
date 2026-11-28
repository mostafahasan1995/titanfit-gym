using Gym.Domain;
using static Gym.Tests.Domain.Dates;

namespace Gym.Tests.Domain;

/// <summary>Money in US dollars, the amount in Syrian pounds at the day's rate, and the rate's state.</summary>
public sealed class CurrencyTests
{
    [Theory]
    [InlineData("20", "11500", 1, "230000")]
    [InlineData("1.25", "11500", 1, "14375")]
    [InlineData("0.33", "11500", 1, "3795")]
    [InlineData("1.25", "11480", 1, "14350")]
    [InlineData("0.01", "11455.5", 1, "115")] // 114.555 → 115
    [InlineData("1.25", "11500", 500, "14500")] // 14,375 → nearest 500
    [InlineData("1.25", "11500", 1000, "14000")] // 14,375 → nearest 1,000
    [InlineData("0.13", "11500", 1000, "1000")] // 1,495 → 1,000
    [InlineData("0.13", "11540", 1000, "2000")] // 1,500.2 → 2,000
    public void Pounds_are_the_dollars_times_the_rate_rounded_to_the_step(string usd, string rate, int step, string expected)
    {
        Assert.Equal(Dec(expected), CurrencyRules.ToSyp(Dec(usd), Dec(rate), step));
    }

    [Fact]
    public void A_half_rounds_up_and_a_step_below_one_counts_as_one()
    {
        Assert.Equal(14_500m, CurrencyRules.ToSyp(1.25m, 11_500m, 250)); // 57.5 steps → 58
        Assert.Equal(2m, CurrencyRules.ToSyp(0.01m, 150m, 1)); // 1.5 → 2
        Assert.Equal(230_000m, CurrencyRules.ToSyp(20m, 11_500m, 0));
        Assert.Equal(0m, CurrencyRules.ToSyp(0m, 11_500m, 500));
    }

    [Fact]
    public void Dollars_keep_two_decimals()
    {
        Assert.Equal(1.13m, CurrencyRules.RoundUsd(1.125m));
        Assert.Equal(20m, CurrencyRules.ToUsd(230_000m, 11_500m));
        Assert.Equal(0m, CurrencyRules.ToUsd(1_000m, 0m));
        Assert.Equal(12.35m, CurrencyRules.CheckUsd(12.345m, "Price"));
        Assert.Equal("validation", Assert.Throws<DomainException>(() => CurrencyRules.CheckUsd(-0.01m, "Price")).Code);
    }

    [Fact]
    public void Paying_in_pounds_uses_the_rate_and_the_step()
    {
        var due = CurrencyRules.Due(20m, Currency.Syp, 11_480m, 500);

        Assert.Equal(new PaymentAmount(Currency.Syp, 229_500m, 11_480m), due); // 229,600 → nearest 500
    }

    [Fact]
    public void Paying_in_dollars_keeps_the_dollars_and_the_rate_when_there_is_one()
    {
        Assert.Equal(new PaymentAmount(Currency.Usd, 20m, 11_500m), CurrencyRules.Due(20m, Currency.Usd, 11_500m));
        Assert.Equal(new PaymentAmount(Currency.Usd, 20m, null), CurrencyRules.Due(20m, Currency.Usd, null));
    }

    [Fact]
    public void Paying_in_pounds_without_any_rate_is_refused_but_dollars_still_work()
    {
        Assert.Equal("no_exchange_rate", Assert.Throws<DomainException>(() => CurrencyRules.Due(20m, Currency.Syp, null)).Code);
        Assert.Equal("no_exchange_rate", Assert.Throws<DomainException>(() => CurrencyRules.Due(20m, Currency.Syp, 0m)).Code);
        Assert.Equal(Currency.Usd, CurrencyRules.Due(20m, Currency.Usd, null).Currency);
        Assert.Equal("validation", Assert.Throws<DomainException>(() => CurrencyRules.Due(20m, (Currency)9, 1m)).Code);
    }

    [Fact]
    public void The_rate_state_is_today_stale_or_none()
    {
        var today = D("2026-11-28");

        Assert.Equal(ExchangeRateState.Today, CurrencyRules.GetState(today, today));
        Assert.Equal(ExchangeRateState.Stale, CurrencyRules.GetState(today.AddDays(-1), today));
        Assert.Equal(ExchangeRateState.Stale, CurrencyRules.GetState(today.AddDays(-30), today));
        Assert.Equal(ExchangeRateState.None, CurrencyRules.GetState(null, today));
    }

    [Fact]
    public void A_rate_must_be_above_zero()
    {
        var rate = ExchangeRate.Create(11_500.456m, D("2026-11-28"), 3, new DateTime(2026, 11, 28, 6, 0, 0, DateTimeKind.Utc));

        Assert.Equal((11_500.46m, D("2026-11-28"), 3), (rate.SypPerUsd, rate.Date, rate.EnteredById));
        Assert.Equal("invalid_exchange_rate", Assert.Throws<DomainException>(() => CurrencyRules.CheckRate(0m)).Code);
        Assert.Equal("invalid_exchange_rate", Assert.Throws<DomainException>(() => CurrencyRules.CheckRate(-5m)).Code);
    }

    [Fact]
    public void A_subscription_paid_in_pounds_keeps_the_dollars_the_pounds_and_the_rate()
    {
        var payment = CurrencyRules.Due(55m, Currency.Syp, 11_500m);
        var subscription = Subscription.Start(Guid.NewGuid(), SubscriptionKind.Membership, 3, 55m, null, D("2026-11-28"), 7, DateTime.UtcNow, 2, payment);

        Assert.Equal(55m, subscription.PricePaid);
        Assert.Equal((Currency.Syp, 632_500m, (decimal?)11_500m), (subscription.Currency, subscription.PaidAmount, subscription.ExchangeRate));
    }

    [Fact]
    public void A_sale_paid_in_pounds_takes_the_cash_and_gives_the_change_in_pounds()
    {
        var sale = new Sale();
        sale.AddLine(new Product { Id = 1, Price = 1.25m }, 2);
        var due = CurrencyRules.Due(sale.Total, Currency.Syp, 11_500m, 500);

        Assert.Equal("cash_received_too_low", Assert.Throws<DomainException>(() => sale.SetPayment(due, 28_000m)).Code);
        sale.SetPayment(due, 30_000m);

        Assert.Equal(2.50m, sale.Total);
        Assert.Equal((Currency.Syp, 29_000m, (decimal?)11_500m), (sale.Currency, sale.PaidAmount, sale.ExchangeRate)); // 28,750 → 29,000
        Assert.Equal(1_000m, sale.Change);
    }

    private static decimal Dec(string value) => decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
