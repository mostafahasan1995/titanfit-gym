using System.Net;
using Gym.Client;
using Gym.Client.Sample;
using Gym.Contracts;
using Gym.Domain;

namespace Gym.Tests.Foundation;

/// <summary>The sample data behind the smoke screenshots follows the same rules as the server.</summary>
public sealed class SampleGymApiTests
{
    private readonly SampleGymApi api = new();

    [Fact]
    public async Task Members_cover_every_status_with_and_without_photos()
    {
        var members = await AllMembersAsync();

        Assert.True(members.Count >= 20);
        var statuses = members.Select(m => m.Membership?.Status).Distinct().ToList();
        Assert.Contains(SubscriptionStatus.Active, statuses);
        Assert.Contains(SubscriptionStatus.ExpiringSoon, statuses);
        Assert.Contains(SubscriptionStatus.Expired, statuses);
        Assert.Contains(null, statuses);
        Assert.Contains(members, m => m.PhotoUrl is not null);
        Assert.Contains(members, m => m.PhotoUrl is null);
        Assert.Contains(members, m => m.FullName.Any(c => c is >= '؀' and <= 'ۿ'));
        Assert.Contains(members, m => m.FullName.Any(char.IsAsciiLetter));
        Assert.Equal(members.Count, members.Select(m => m.MemberNo).Distinct().Count());
        Assert.Equal(members.Count, members.Select(m => m.QrCode).Distinct().Count());
    }

    [Fact]
    public async Task Status_follows_the_latest_membership_end_date()
    {
        var settings = await api.GetSettingsAsync();

        foreach (var member in await AllMembersAsync())
        {
            var payments = await api.GetMemberSubscriptionsAsync(member.Id);
            Assert.All(payments, p => Assert.Equal(SubscriptionRules.CalculateEndDate(p.StartDate, p.Months), p.EndDate));
            var memberships = payments.Where(p => p.Kind == SubscriptionKind.Membership).ToList();
            if (memberships.Count == 0)
            {
                Assert.Null(member.Membership);
                continue;
            }

            var end = memberships.Max(p => p.EndDate);
            var state = SubscriptionRules.GetStatus(end, api.Today, settings.ExpiringSoonDays);
            Assert.NotNull(member.Membership);
            Assert.Equal((end, state.Status, state.DaysLeft, state.DaysOverdue),
                (member.Membership.EndDate, member.Membership.Status, member.Membership.DaysLeft, member.Membership.DaysOverdue));
        }
    }

    [Fact]
    public async Task Photo_urls_return_png_images()
    {
        foreach (var member in await AllMembersAsync())
        {
            if (member.PhotoUrl is { } url)
            {
                var bytes = await api.GetBytesAsync(url);
                Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes.Take(4));
                Assert.NotNull(await api.GetMemberPhotoAsync(member.Id));
            }
            else
            {
                Assert.Null(await api.GetMemberPhotoAsync(member.Id));
            }
        }
    }

    [Fact]
    public async Task Private_trainers_and_their_clients_match()
    {
        var trainers = await api.GetPrivateTrainersAsync();

        Assert.True(trainers.Count >= 2);
        Assert.True(trainers.Sum(t => t.ClientCount) >= 3);
        foreach (var trainer in trainers)
        {
            var clients = await api.GetPrivateTrainerClientsAsync(trainer.Id);
            Assert.Equal(trainer.ClientCount, clients.Count);
            Assert.All(clients, c => Assert.Equal(trainer.Id, c.PrivateTrainerId));
            Assert.NotNull(trainer.PrivateService);
            Assert.Equal(MemberType.PrivateTrainer, (await api.GetMemberAsync(trainer.Id)).Type);
        }
    }

    [Fact]
    public async Task Products_cover_the_four_categories_and_stock_is_the_sum_of_movements()
    {
        var categories = await api.GetCategoriesAsync();
        var products = await api.GetProductsAsync(includeInactive: true);
        var movements = await api.GetStockMovementsAsync();

        Assert.Equal(new[] { "Drinks", "Shakes", "Snacks", "Supplements" }, categories.Select(c => c.NameEn));
        Assert.All(categories, c => Assert.Contains(products, p => p.CategoryId == c.Id && p.IsActive));
        foreach (var product in products)
        {
            Assert.Equal(movements.Where(m => m.ProductId == product.Id).Sum(m => m.Quantity), product.Stock);
            Assert.True(product.Stock >= 0);
            Assert.Equal(product.Stock < product.MinStock, product.IsLowStock);
        }

        Assert.Contains(products, p => p.IsLowStock);
        Assert.Contains(products, p => !p.IsActive);
        Assert.Equal(products.Count(p => p.IsActive && p.IsLowStock), (await api.GetStockAsync(lowOnly: true)).Count);
        var withBarcode = products.First(p => p.Barcode is not null);
        Assert.Equal(withBarcode.Id, (await api.FindProductByBarcodeAsync(withBarcode.Barcode!))?.Id);
    }

    [Fact]
    public async Task Sales_add_up_and_belong_to_a_shift()
    {
        var sales = await api.GetSalesAsync();
        var shifts = await api.GetShiftsAsync();
        var open = Assert.Single(shifts, s => s.ClosedAt is null);

        Assert.True(sales.Count > 20);
        var inOpenShift = 0;
        foreach (var sale in sales)
        {
            var receipt = await api.GetSaleAsync(sale.Id);
            Assert.All(receipt.Lines, l => Assert.Equal(l.UnitPrice * l.Quantity, l.LineTotal));
            Assert.Equal(receipt.Lines.Sum(l => l.LineTotal), receipt.Total);
            Assert.Contains(shifts, s => s.Id == receipt.ShiftId);
            Assert.True(receipt.CashReceived >= receipt.PaidAmount);
            Assert.Equal(receipt.CashReceived - receipt.PaidAmount, receipt.Change);
            if (receipt.Currency == Currency.Syp)
            {
                Assert.Equal(CurrencyRules.ToSyp(receipt.Total, receipt.ExchangeRate!.Value), receipt.PaidAmount);
            }
            else
            {
                Assert.Equal(receipt.Total, receipt.PaidAmount);
            }

            inOpenShift += receipt.ShiftId == open.Id ? 1 : 0;
        }

        var summary = await api.GetShiftSummaryAsync(open.Id);
        Assert.Equal(inOpenShift, summary.SaleCount);
        Assert.Equal(open.OpeningCash + summary.SalesTotal, summary.ExpectedCash);
        Assert.Equal(summary.SalesTotal, summary.Paid!.Usd + summary.Paid.SypAsUsd);
        Assert.Contains(sales, s => s.Currency == Currency.Syp);
        Assert.Contains(sales, s => s.Currency == Currency.Usd);
    }

    [Fact]
    public async Task The_dashboard_matches_the_data()
    {
        var dashboard = await api.GetDashboardAsync();
        var members = await AllMembersAsync();
        var todaysSales = await api.GetSalesAsync(api.Today, api.Today);

        Assert.Equal(members.Count(m => m.Membership?.Status == SubscriptionStatus.Active), dashboard.ActiveMembers);
        Assert.Equal(members.Count(m => m.Membership?.Status == SubscriptionStatus.ExpiringSoon), dashboard.ExpiringSoonMembers);
        Assert.Equal(members.Count(m => m.Membership?.Status == SubscriptionStatus.Expired), dashboard.ExpiredMembers);
        Assert.Equal(24, dashboard.CheckInsByHour.Count);
        Assert.True(dashboard.CheckInsToday > 0);
        Assert.Equal(dashboard.CheckInsToday, dashboard.CheckInsByHour.Sum(h => h.Count));
        Assert.Equal(todaysSales.Sum(s => s.Total), dashboard.RevenueCafeteria);
        Assert.True(dashboard.RevenueSubscriptions > 0);
        Assert.Equal(dashboard.RevenueSubscriptions + dashboard.RevenuePrivateTraining + dashboard.RevenueCafeteria, dashboard.RevenueTotal);
        Assert.NotEmpty(dashboard.LowStock);
        Assert.NotEmpty(dashboard.ExpiringSoon);
        Assert.All(dashboard.ExpiringSoon, m => Assert.Equal(SubscriptionStatus.ExpiringSoon, m.Membership?.Status));
    }

    [Fact]
    public async Task Sample_logins_work_and_wrong_ones_are_refused()
    {
        foreach (var role in Enum.GetValues<StaffRole>())
        {
            var login = await api.LoginAsync(SampleGymApi.UsernameFor(role), SampleGymApi.SamplePassword);
            Assert.Equal(role, login.Staff.Role);
            Assert.Equal(login.Token, api.AccessToken);
        }

        var wrong = await Assert.ThrowsAsync<GymApiException>(() => api.LoginAsync("sara", "wrong"));
        Assert.Equal((HttpStatusCode.Unauthorized, "invalid_login"), (wrong.StatusCode, wrong.Code));
        await Assert.ThrowsAsync<GymApiException>(() => api.LoginAsync("wael", SampleGymApi.SamplePassword)); // inactive
    }

    [Fact]
    public async Task Club_trainers_have_no_login_and_monthly_ratings()
    {
        var trainers = await api.GetStaffAsync(StaffType.ClubTrainer);

        Assert.True(trainers.Count >= 3);
        foreach (var trainer in trainers)
        {
            Assert.False(trainer.CanLogIn);
            var ratings = await api.GetRatingsAsync(trainer.Id);
            Assert.NotEmpty(ratings);
            Assert.All(ratings, r => Assert.InRange(r.Stars, 1, 5));
        }
    }

    [Fact]
    public async Task Settings_and_plans_have_the_sample_prices()
    {
        var settings = await api.GetSettingsAsync();
        var plans = await api.GetPlansAsync();

        Assert.Equal(new SettingsDto(3, 25m, 1), settings);
        Assert.Equal(new[] { 1, 3, 6, 12 }, plans.Select(p => p.Months));
        Assert.Equal(new[] { 20m, 55m, 100m, 180m }, plans.Select(p => p.Price));
    }

    [Fact]
    public async Task The_sample_has_today_s_rate_and_a_month_of_history()
    {
        var status = await api.GetExchangeRateAsync();
        var history = await api.GetExchangeRatesAsync();

        Assert.Equal((ExchangeRateState.Today, (decimal?)SampleGymApi.SampleRate), (status.State, status.Rate?.SypPerUsd));
        Assert.Equal(30, history.Count);
        Assert.Equal(status.Rate, history[0]);
    }

    [Fact]
    public async Task Without_today_s_rate_the_last_one_is_used_and_without_any_only_dollars_work()
    {
        await api.LoginAsync(SampleGymApi.UsernameFor(StaffRole.Reception), SampleGymApi.SamplePassword);
        var member = (await AllMembersAsync()).First(m => m.Membership?.Status == SubscriptionStatus.Active);
        var plan = (await api.GetPlansAsync())[0];

        api.SimulateExchangeRate(ExchangeRateState.Stale);
        var stale = await api.GetExchangeRateAsync();
        Assert.Equal(ExchangeRateState.Stale, stale.State);
        var paid = await api.RenewMemberAsync(member.Id, plan.Id, new PaymentRequest(Currency.Syp));
        Assert.Equal((Currency.Syp, (decimal?)stale.Rate!.SypPerUsd), (paid.Currency, paid.ExchangeRate));
        Assert.Equal(CurrencyRules.ToSyp(plan.Price, stale.Rate.SypPerUsd), paid.PaidAmount);

        api.SimulateExchangeRate(ExchangeRateState.None);
        Assert.Equal((ExchangeRateState.None, (ExchangeRateDto?)null), ((await api.GetExchangeRateAsync()).State, (await api.GetExchangeRateAsync()).Rate));
        var refused = await Assert.ThrowsAsync<GymApiException>(() => api.RenewMemberAsync(member.Id, plan.Id, new PaymentRequest(Currency.Syp)));
        Assert.Equal("no_exchange_rate", refused.Code);
        var dollars = await api.RenewMemberAsync(member.Id, plan.Id);
        Assert.Equal((Currency.Usd, plan.Price, (decimal?)null), (dollars.Currency, dollars.PaidAmount, dollars.ExchangeRate));
    }

    [Fact]
    public async Task Entering_a_rate_publishes_it_and_rounds_with_the_step()
    {
        ExchangeRateStatusDto? published = null;
        api.Live.ExchangeRateChanged += e => published = e;
        await api.LoginAsync(SampleGymApi.UsernameFor(StaffRole.Admin), SampleGymApi.SamplePassword);
        await api.UpdateSettingsAsync(new SettingsDto(3, 25m, 500));

        var rate = await api.SetExchangeRateAsync(new SetExchangeRateRequest(11_480m));

        Assert.Equal(rate, published?.Rate);
        Assert.Equal(500, published?.SypRoundingStep);
        await api.LoginAsync(SampleGymApi.UsernameFor(StaffRole.Cashier), SampleGymApi.SamplePassword);
        var product = (await api.GetProductsAsync()).First(p => p.Stock >= 1 && p.Price == 1.50m);
        var receipt = await api.CreateSaleAsync(new CreateSaleRequest("Cafeteria 1", [new SaleLineRequest(product.Id, 1)], 20_000m, new PaymentRequest(Currency.Syp, rate.Id)));
        Assert.Equal((Currency.Syp, 17_000m, 3_000m), (receipt.Currency, receipt.PaidAmount, receipt.Change)); // 17,220 → 17,000
        await Assert.ThrowsAsync<GymApiException>(() => api.SetExchangeRateAsync(new SetExchangeRateRequest(0m)));
    }

    [Fact]
    public async Task Search_finds_members_by_name_phone_and_number()
    {
        Assert.Contains((await api.SearchMembersAsync("الخطيب")).Items, m => m.FullName.StartsWith("عمر", StringComparison.Ordinal));
        Assert.Contains((await api.SearchMembersAsync("احمد")).Items, m => m.FullName.Contains("أحمد", StringComparison.Ordinal));
        Assert.Single((await api.SearchMembersAsync("0944123456")).Items);
        Assert.Equal(231, Assert.Single((await api.SearchMembersAsync("#00231")).Items).MemberNo);
        Assert.Empty((await api.SearchMembersAsync("no such member")).Items);
        var expired = await api.SearchMembersAsync(status: SubscriptionStatus.Expired, pageSize: 100);
        Assert.All(expired.Items, m => Assert.Equal(SubscriptionStatus.Expired, m.Membership?.Status));
    }

    [Fact]
    public async Task Selling_lowers_the_stock_and_publishes_live_events()
    {
        StockChangedEvent? stock = null;
        SaleCompletedEvent? sold = null;
        api.Live.StockChanged += e => stock = e;
        api.Live.SaleCompleted += e => sold = e;
        await api.LoginAsync(SampleGymApi.UsernameFor(StaffRole.Cashier), SampleGymApi.SamplePassword);
        var product = (await api.GetProductsAsync()).First(p => p.Stock >= 2);

        var receipt = await api.CreateSaleAsync(new CreateSaleRequest("Cafeteria 1", [new SaleLineRequest(product.Id, 2)], product.Price * 2 + 1m));

        Assert.Equal(product.Stock - 2, (await api.GetProductAsync(product.Id)).Stock);
        Assert.Equal(1m, receipt.Change);
        Assert.Equal(product.Stock - 2, Assert.Single(stock!.Products).Stock);
        Assert.Equal(receipt.Id, sold?.SaleId);
        var tooMany = await Assert.ThrowsAsync<GymApiException>(() =>
            api.CreateSaleAsync(new CreateSaleRequest("Cafeteria 1", [new SaleLineRequest(product.Id, 10_000)])));
        Assert.Equal("insufficient_stock", tooMany.Code);
    }

    [Fact]
    public async Task Early_renewal_starts_the_day_after_the_current_end_date()
    {
        await api.LoginAsync(SampleGymApi.UsernameFor(StaffRole.Reception), SampleGymApi.SamplePassword);
        var member = (await AllMembersAsync()).First(m => m.Membership?.Status == SubscriptionStatus.Active);
        var plan = (await api.GetPlansAsync())[0];

        var renewal = await api.RenewMemberAsync(member.Id, plan.Id);

        Assert.Equal(member.Membership!.EndDate.AddDays(1), renewal.StartDate);
        Assert.Equal(renewal.EndDate, (await api.GetMemberAsync(member.Id)).Membership?.EndDate);
    }

    [Fact]
    public async Task Check_in_gives_the_member_status_and_publishes_it()
    {
        CheckInResultDto? published = null;
        api.Live.CheckInRecorded += e => published = e;
        var expired = (await AllMembersAsync()).First(m => m.Membership?.Status == SubscriptionStatus.Expired);

        var byNumber = await api.CheckInAsync(new CheckInRequest("Reception 1", MemberNo: expired.MemberNo));
        var unknown = await api.CheckInAsync(new CheckInRequest("Reception 1", Code: "TF1-NOBODY"));

        Assert.Equal((CheckInResult.Expired, false, expired.Id), (byNumber.Result, byNumber.Admitted, byNumber.MemberId!.Value));
        Assert.Equal(CheckInResult.UnknownMember, unknown.Result);
        Assert.Equal(unknown.Id, published?.Id);
    }

    private async Task<List<MemberDto>> AllMembersAsync()
    {
        var page = await api.SearchMembersAsync(pageSize: 200);
        var list = new List<MemberDto>();
        foreach (var summary in page.Items)
        {
            list.Add(await api.GetMemberAsync(summary.Id));
        }

        return list;
    }
}
