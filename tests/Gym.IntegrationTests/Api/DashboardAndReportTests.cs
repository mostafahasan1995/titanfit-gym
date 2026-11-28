namespace Gym.IntegrationTests.Api;

/// <summary>Own database (class fixture), so the dashboard counts are exact.</summary>
public sealed class DashboardAndReportTests(ApiFactory fx) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Dashboard_and_reports_add_up()
    {
        var today = TestTimeProvider.DefaultStart;
        var day = DateOnly.FromDateTime(today.UtcDateTime);
        var admin = await fx.AdminAsync();
        var reception = await fx.ReceptionAsync();
        var cashier = await fx.CashierAsync();

        // Expired: 1 month bought 40 days ago. Expiring soon: 1 month bought so it ends in 2 days. Active: 3 months today.
        fx.Time.SetToday(day.AddDays(-40));
        var expired = await reception.NewMemberAsync(planMonths: 1, firstName: "Expired");
        fx.Time.SetToday(day.AddMonths(-1).AddDays(2));
        var expiring = await reception.NewMemberAsync(planMonths: 1, firstName: "Expiring");
        fx.Time.Set(today);
        var active = await reception.NewMemberAsync(planMonths: 3, firstName: "Active");

        Assert.Equal(CheckInResult.Active, (await reception.CheckInAsync(new CheckInRequest("Reception 1", Code: active.QrCode))).Result);
        Assert.Equal(CheckInResult.Expired, (await reception.CheckInAsync(new CheckInRequest("Reception 1", Code: expired.QrCode))).Result);

        var water = await cashier.NewProductAsync(price: 0.50m, stock: 3, minStock: 5);
        await cashier.CreateSaleAsync(new CreateSaleRequest("Cafeteria 1", [new(water.Id, 2)]));

        var dashboard = await admin.GetDashboardAsync();
        Assert.Equal(day, dashboard.Date);
        Assert.Equal((1, 1), (dashboard.CheckInsToday, dashboard.DeniedToday));
        Assert.Equal(24, dashboard.CheckInsByHour.Count);
        Assert.Equal(1, dashboard.CheckInsByHour.Single(h => h.Hour == 9).Count);
        Assert.Equal((1, 1, 1), (dashboard.ActiveMembers, dashboard.ExpiringSoonMembers, dashboard.ExpiredMembers));
        Assert.Equal(expiring.Id, Assert.Single(dashboard.ExpiringSoon).Id);
        Assert.Equal(55m, dashboard.RevenueSubscriptions); // only today's payment (3 months, the default $55)
        Assert.Equal(1.00m, dashboard.RevenueCafeteria);
        Assert.Equal(56.00m, dashboard.RevenueTotal);
        Assert.Equal(water.Id, Assert.Single(dashboard.LowStock).ProductId);

        // With a threshold of 1 day, 2 days left is active.
        await admin.UpdateSettingsAsync(new SettingsDto(1, 25m));
        var strict = await admin.GetDashboardAsync();
        Assert.Equal((2, 0, 1), (strict.ActiveMembers, strict.ExpiringSoonMembers, strict.ExpiredMembers));
        await admin.UpdateSettingsAsync(new SettingsDto(3, 25m));

        var subscriptions = await admin.GetSubscriptionReportAsync(day.AddDays(-60), day);
        Assert.Equal((3, 95m), (subscriptions.Count, subscriptions.Total));
        Assert.Equal(new[] { (1, 2, 40m), (3, 1, 55m) }, subscriptions.ByPlan.Select(r => (r.Months, r.Count, r.Total)));
        Assert.Equal(new CurrencyTotals(95m, 0m, 0m, 3, 0), subscriptions.Paid);
        Assert.Equal(3, subscriptions.ByDay.Count);

        var attendance = await admin.GetAttendanceReportAsync(day, day);
        Assert.Equal((2, 1, 1, 1), (attendance.TotalScans, attendance.Admitted, attendance.Denied, attendance.UniqueMembers));

        var sales = await admin.GetSalesReportAsync(day, day);
        Assert.Equal((1, 1.00m), (sales.SaleCount, sales.Total));
        Assert.Equal((water.Id, 2), (Assert.Single(sales.ByProduct).ProductId, sales.ByProduct[0].Quantity));
        Assert.Single(sales.ByCashier);

        var badRange = await Assert.ThrowsAsync<Gym.Client.GymApiException>(() => admin.GetSalesReportAsync(day, day.AddDays(-1)));
        Assert.Equal("invalid_range", badRange.Code);
    }
}
