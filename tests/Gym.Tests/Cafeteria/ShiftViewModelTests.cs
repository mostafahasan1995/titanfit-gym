using Gym.Cafeteria.Sales;
using Gym.Client.Sample;
using Gym.UI;

namespace Gym.Tests.Cafeteria;

public sealed class ShiftViewModelTests
{
    private static async Task<(SampleGymApi Api, ShiftViewModel Model)> StartAsync()
    {
        var api = new SampleGymApi();
        var login = await api.LoginAsync("ahmad", SampleGymApi.SamplePassword);
        var model = new ShiftViewModel(api, () => "Cafeteria 1", login.Staff.Id, AppLanguage.English);
        await model.LoadAsync();
        return (api, model);
    }

    [Fact]
    public async Task The_open_shift_is_shown_with_its_totals_and_the_past_shifts_are_listed()
    {
        var (_, model) = await StartAsync();

        Assert.NotNull(model.Current);
        Assert.Same(model.Current, model.Shown);
        Assert.Equal(14, model.Current.SaleCount);
        Assert.Equal(model.Current.Shift.OpeningCash + model.Current.SalesTotal, model.Current.ExpectedCash);
        Assert.NotEmpty(model.Current.Products);
        Assert.True(model.History[0].IsOpen);
        Assert.Contains(model.History, h => !h.IsOpen);
        Assert.All(model.History, h => Assert.Equal("أحمد قاسم", h.Cashier));
        Assert.Equal("14 sales", model.History[0].SalesText);
    }

    [Fact]
    public async Task Closing_the_shift_keeps_the_counted_cash_and_the_difference()
    {
        var (_, model) = await StartAsync();
        var expected = model.Current!.ExpectedCash;

        var closed = await model.CloseAsync(expected - 2.50m, "  short by $2.50 ");

        Assert.NotNull(closed.Shift.ClosedAt);
        Assert.Equal(-2.50m, closed.CashDifference);
        Assert.Equal("short by $2.50", closed.Shift.Note);
        Assert.Null(model.Current);
        Assert.Same(closed, model.Shown);
        var item = model.History.First(h => h.Id == closed.Shift.Id);
        Assert.False(item.IsOpen);
        Assert.Equal("Cash short", item.StatusText);
    }

    [Fact]
    public async Task A_new_shift_can_be_opened_with_cash_in_the_drawer()
    {
        var (_, model) = await StartAsync();
        await model.CloseAsync(null, null);

        var shift = await model.OpenAsync(20m);

        Assert.Equal("Cafeteria 1", shift.Station);
        Assert.Equal(shift.Id, model.Current?.Shift.Id);
        Assert.Equal(0, model.Current!.SaleCount);
        Assert.Equal(20m, model.Current.ExpectedCash);
    }

    [Theory]
    [InlineData("265.50", "263.50", "-2.00")]
    [InlineData("265.50", "265.50", "0")]
    [InlineData("265.50", "268.75", "3.25")]
    [InlineData("265.50", null, null)]
    public void The_difference_is_counted_minus_expected(string expected, string? counted, string? difference)
    {
        Assert.Equal(D(difference), ShiftViewModel.Difference(D(expected)!.Value, D(counted)));
    }

    [Fact]
    public async Task The_shift_summary_splits_what_was_taken_in_dollars_and_in_pounds()
    {
        var (_, model) = await StartAsync();
        var paid = model.Current!.Paid!;

        // The sample pays every third sale in pounds; dollars + the pounds' dollar value = the sales total.
        Assert.True(paid.SypCount > 0);
        Assert.True(paid.Syp > 0);
        Assert.Equal(model.Current.SalesTotal, paid.Usd + paid.SypAsUsd);
        Assert.Equal(model.Current.SaleCount, paid.UsdCount + paid.SypCount);
    }

    private static decimal? D(string? text) => text is null ? null : decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
}
