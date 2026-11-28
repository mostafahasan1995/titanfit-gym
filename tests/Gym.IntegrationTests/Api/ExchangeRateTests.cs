using System.Net;
using Gym.Client;

namespace Gym.IntegrationTests.Api;

/// <summary>
/// Money in US dollars, the daily exchange rate and payments in pounds, through the real API on its own database
/// (class fixture: the tests run in order on one rate history).
/// </summary>
public sealed class ExchangeRateTests(ApiFactory fx) : IClassFixture<ApiFactory>
{
    /// <summary>One story on one rate history: no rate, today's rate, the next day (stale), the rounding step, the reports.</summary>
    [Fact]
    public async Task Rates_payments_and_reports_from_no_rate_to_a_stale_rate()
    {
        await NoRateYetOnlyDollarsCanBeTakenAsync();
        await TodaysRateAndAPaymentInPoundsAreStoredAsync();
        await NextDayTheLastRateIsStillUsedAndStaleAsync();
        await SaleInPoundsUsesTheStepAndGivesChangeInPoundsAsync();
        await SubscriptionReportTotalsDollarsAndShowsThePoundsAsync();
    }

    private async Task NoRateYetOnlyDollarsCanBeTakenAsync()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var reception = await fx.ReceptionAsync();
        var member = await reception.NewMemberAsync(planMonths: null);
        var plan = await reception.PlanIdAsync(1);

        var status = await reception.GetExchangeRateAsync();
        Assert.Equal((ExchangeRateState.None, (ExchangeRateDto?)null, fx.Today), (status.State, status.Rate, status.Today));

        var refused = await Assert.ThrowsAsync<GymApiException>(() => reception.RenewMemberAsync(member.Id, plan, new PaymentRequest(Currency.Syp)));
        Assert.Equal((HttpStatusCode.BadRequest, "no_exchange_rate"), (refused.StatusCode, refused.Code));

        var paid = await reception.RenewMemberAsync(member.Id, plan, new PaymentRequest(Currency.Usd));
        Assert.Equal((20m, Currency.Usd, 20m, (decimal?)null), (paid.PricePaid, paid.Currency, paid.PaidAmount, paid.ExchangeRate));
        Assert.Empty((await (await fx.AdminAsync()).GetExchangeRatesAsync()));
    }

    private async Task TodaysRateAndAPaymentInPoundsAreStoredAsync()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var admin = await fx.AdminAsync();
        var reception = await fx.ReceptionAsync();
        var cashier = await fx.CashierAsync();

        await AssertForbiddenAsync(() => reception.SetExchangeRateAsync(new SetExchangeRateRequest(11_500m)));
        await AssertForbiddenAsync(() => cashier.SetExchangeRateAsync(new SetExchangeRateRequest(11_500m)));
        await AssertForbiddenAsync(() => cashier.GetExchangeRatesAsync());
        var invalid = await Assert.ThrowsAsync<GymApiException>(() => admin.SetExchangeRateAsync(new SetExchangeRateRequest(0m)));
        Assert.Equal("invalid_exchange_rate", invalid.Code);

        await admin.SetExchangeRateAsync(new SetExchangeRateRequest(11_400m));
        fx.Time.Advance(TimeSpan.FromMinutes(5));
        var rate = await admin.SetExchangeRateAsync(new SetExchangeRateRequest(11_500m)); // a correction the same day
        Assert.Equal((fx.Today, 11_500m), (rate.Date, rate.SypPerUsd));
        Assert.Equal("Owner", rate.EnteredBy);

        var status = await cashier.GetExchangeRateAsync();
        Assert.Equal((ExchangeRateState.Today, rate), (status.State, status.Rate));
        Assert.Equal(new[] { 11_500m, 11_400m }, (await admin.GetExchangeRatesAsync()).Select(r => r.SypPerUsd));

        // A new member pays 3 months ($55) in pounds at the rate the station showed.
        var member = await reception.CreateMemberAsync(new CreateMemberRequest(
            "Rami", "Saleh", NationalNo: TestData.NationalNo(), PlanId: await reception.PlanIdAsync(3),
            Payment: new PaymentRequest(Currency.Syp, rate.Id)));
        var paid = Assert.Single(await reception.GetMemberSubscriptionsAsync(member.Id));
        Assert.Equal((55m, Currency.Syp, 632_500m, (decimal?)11_500m), (paid.PricePaid, paid.Currency, paid.PaidAmount, paid.ExchangeRate));

        // Stored as such in the database.
        await using var db = fx.CreateDbContext();
        var row = await db.Subscriptions.SingleAsync(s => s.Id == paid.Id);
        Assert.Equal((55m, Currency.Syp, 632_500m, (decimal?)11_500m), (row.PricePaid, row.Currency, row.PaidAmount, row.ExchangeRate));

        var missing = await Assert.ThrowsAsync<GymApiException>(() => reception.RenewMemberAsync(member.Id, paid.PlanId!.Value, new PaymentRequest(Currency.Syp, 999_999)));
        Assert.Equal("exchange_rate_not_found", missing.Code);
    }

    private async Task NextDayTheLastRateIsStillUsedAndStaleAsync()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart.AddDays(1));
        var reception = await fx.ReceptionAsync();
        var trainer = await reception.NewMemberAsync(type: MemberType.PrivateTrainer);

        var status = await reception.GetExchangeRateAsync();
        Assert.Equal((ExchangeRateState.Stale, 11_500m, fx.Today), (status.State, status.Rate!.SypPerUsd, status.Today));

        var service = await reception.AddPrivateServiceAsync(trainer.Id, 2, new PaymentRequest(Currency.Syp));
        Assert.Equal((50m, Currency.Syp, 575_000m, (decimal?)11_500m), (service.PricePaid, service.Currency, service.PaidAmount, service.ExchangeRate));
    }

    private async Task SaleInPoundsUsesTheStepAndGivesChangeInPoundsAsync()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart.AddDays(2));
        var admin = await fx.AdminAsync();
        var cashier = await fx.CashierAsync();
        var before = await admin.GetSettingsAsync();
        await admin.UpdateSettingsAsync(before with { SypRoundingStep = 500 });
        try
        {
            var rate = await admin.SetExchangeRateAsync(new SetExchangeRateRequest(11_480m));
            Assert.Equal(500, (await cashier.GetExchangeRateAsync()).SypRoundingStep);
            var shake = await cashier.NewProductAsync(price: 1.25m, stock: 10);
            var shift = await cashier.OpenShiftAsync(new OpenShiftRequest("Cafeteria 1", OpeningCash: 10m));

            // 2 × $1.25 = $2.50 → 28,700 pounds → 28,500 (nearest 500).
            var tooLow = await Assert.ThrowsAsync<GymApiException>(() => cashier.CreateSaleAsync(
                new CreateSaleRequest("Cafeteria 1", [new(shake.Id, 2)], 28_000m, new PaymentRequest(Currency.Syp, rate.Id))));
            Assert.Equal("cash_received_too_low", tooLow.Code);
            var receipt = await cashier.CreateSaleAsync(new CreateSaleRequest("Cafeteria 1", [new(shake.Id, 2)], 30_000m, new PaymentRequest(Currency.Syp, rate.Id)));
            Assert.Equal((2.50m, Currency.Syp, 28_500m, (decimal?)11_480m), (receipt.Total, receipt.Currency, receipt.PaidAmount, receipt.ExchangeRate));
            Assert.Equal((30_000m, 1_500m), (receipt.CashReceived!.Value, receipt.Change!.Value));

            var dollars = await cashier.CreateSaleAsync(new CreateSaleRequest("Cafeteria 1", [new(shake.Id, 1)], 5m));
            Assert.Equal((Currency.Usd, 1.25m, 3.75m), (dollars.Currency, dollars.PaidAmount, dollars.Change!.Value));
            Assert.Equal((decimal?)11_480m, dollars.ExchangeRate); // the rate of the day is kept with a dollar payment too

            var summary = await cashier.GetShiftSummaryAsync(shift.Id);
            Assert.Equal((3.75m, 13.75m), (summary.SalesTotal, summary.ExpectedCash));
            Assert.Equal(new CurrencyTotals(1.25m, 28_500m, 2.50m, 1, 1), summary.Paid);

            var report = await admin.GetSalesReportAsync(fx.Today, fx.Today);
            Assert.Equal(3.75m, report.Total);
            Assert.Equal(new CurrencyTotals(1.25m, 28_500m, 2.50m, 1, 1), report.Paid);
            var listed = await cashier.GetSalesAsync(fx.Today, fx.Today);
            Assert.Contains(listed, s => s.Id == receipt.Id && s.Currency == Currency.Syp && s.PaidAmount == 28_500m);
        }
        finally
        {
            await admin.UpdateSettingsAsync(before);
        }

        var invalidStep = await Assert.ThrowsAsync<GymApiException>(() => admin.UpdateSettingsAsync(before with { SypRoundingStep = 0 }));
        Assert.Equal("invalid_setting", invalidStep.Code);
    }

    private async Task SubscriptionReportTotalsDollarsAndShowsThePoundsAsync()
    {
        var admin = await fx.AdminAsync();
        var start = DateOnly.FromDateTime(TestTimeProvider.DefaultStart.UtcDateTime);

        var report = await admin.GetSubscriptionReportAsync(start, start.AddDays(2));

        // A: $20 in dollars; B: $55 in pounds (632,500); C: $50 in pounds (575,000) and the trainer's first month $20 in dollars.
        Assert.Equal(145m, report.Total);
        Assert.Equal(new CurrencyTotals(40m, 1_207_500m, 105m, 2, 2), report.Paid);
    }

    private static async Task AssertForbiddenAsync(Func<Task> call) =>
        Assert.Equal(HttpStatusCode.Forbidden, (await Assert.ThrowsAsync<GymApiException>(call)).StatusCode);
}
