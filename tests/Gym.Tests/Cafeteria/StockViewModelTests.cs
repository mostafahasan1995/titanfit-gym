using Gym.Cafeteria.Inventory;
using Gym.Client;
using Gym.Client.Sample;
using Gym.Contracts;
using Gym.Domain;
using Gym.UI;

namespace Gym.Tests.Cafeteria;

public sealed class StockViewModelTests
{
    private static async Task<(SampleGymApi Api, StockViewModel Model)> StartAsync()
    {
        var api = new SampleGymApi();
        await api.LoginAsync("ahmad", SampleGymApi.SamplePassword);
        var model = new StockViewModel(api, AppLanguage.English);
        await model.LoadAsync();
        return (api, model);
    }

    [Fact]
    public async Task The_list_shows_active_items_and_the_filters_narrow_it()
    {
        var (_, model) = await StartAsync();

        Assert.Equal(16, model.Rows.Count);
        Assert.NotNull(model.Selected);
        Assert.NotEmpty(model.Movements);

        model.LowOnly = true;
        Assert.Equal(model.LowCount, model.Rows.Count);
        Assert.All(model.Rows, r => Assert.True(r.IsLow));
        Assert.Contains(model.Rows, r => r.Id == 5 && r.IsOut && r.StatusText == "Out of stock");

        model.LowOnly = false;
        model.IncludeInactive = true;
        Assert.Equal(17, model.Rows.Count);
        Assert.Equal("Inactive", model.Rows.Single(r => r.Id == 17).StatusText);

        model.CategoryId = 4;
        Assert.Equal(4, model.Rows.Count);

        model.CategoryId = null;
        model.Search = "واي";
        Assert.Equal(13, Assert.Single(model.Rows).Id);
    }

    [Fact]
    public async Task Stock_in_raises_the_stock_and_adds_a_movement()
    {
        var (_, model) = await StartAsync();
        var row = model.Rows.Single(r => r.Id == 2);
        await model.SelectAsync(row);
        var before = row.Stock;

        var level = await model.StockInAsync(2, 24, " Supplier invoice 77 ");

        Assert.Equal(before + 24, level.Stock);
        Assert.Equal(before + 24, row.Stock);
        // The sample's sales of today run until the evening, so the new row is not always the first one.
        var latest = Assert.Single(model.Movements, m => m.Movement.Note == "Supplier invoice 77");
        Assert.Equal(StockMovementType.StockIn, latest.Movement.Type);
        Assert.Equal(24, latest.Movement.Quantity);
        Assert.Equal("Supplier invoice 77", latest.Movement.Note);
        Assert.Equal("‎+24", latest.QuantityText);
    }

    [Fact]
    public async Task An_adjustment_below_zero_is_refused_and_a_stocktake_sets_the_count()
    {
        var (_, model) = await StartAsync();

        var refused = await Assert.ThrowsAsync<GymApiException>(() => model.AdjustAsync(5, -1, null, "broken"));
        Assert.True(refused.Code is "insufficient_stock" or "negative_stock", refused.Code);

        var level = await model.AdjustAsync(2, null, 3, "stocktake");
        Assert.Equal(3, level.Stock);
        Assert.Equal(3, model.Products.Single(p => p.Id == 2).Stock);
    }

    [Fact]
    public async Task A_new_item_is_saved_and_shown_and_a_taken_barcode_is_refused()
    {
        var (_, model) = await StartAsync();

        var saved = await model.SaveProductAsync(null, new SaveProductRequest("Granola bar", "لوح غرانولا", 3, 15_000, "6291001000998", 5));

        Assert.Contains(model.Rows, r => r.Id == saved.Id && r.Name == "Granola bar");
        Assert.Equal(saved.Id, model.Selected?.Id);
        var duplicate = await Assert.ThrowsAsync<GymApiException>(() =>
            model.SaveProductAsync(null, new SaveProductRequest("Copy", "نسخة", 3, 1_000, "6291001000998")));
        Assert.Equal("duplicate_barcode", duplicate.Code);
    }

    [Theory]
    [InlineData("24", 24)]
    [InlineData(" ٢٤ ", 24)]
    [InlineData("1,000", 1000)]
    [InlineData("0", null)]
    [InlineData("-3", null)]
    [InlineData("abc", null)]
    [InlineData("", null)]
    public void Stock_in_quantities_are_whole_numbers_from_one(string text, int? expected)
    {
        Assert.Equal(expected, StockForms.ParseQuantity(text));
    }

    [Theory]
    [InlineData("-2", -2)]
    [InlineData("−2", -2)]
    [InlineData("+5", 5)]
    [InlineData("0", null)]
    [InlineData("x", null)]
    public void Changes_take_a_sign_and_are_not_zero(string text, int? expected)
    {
        Assert.Equal(expected, StockForms.ParseChange(text));
    }

    [Fact]
    public void Counts_can_be_zero_and_signed_numbers_show_their_sign()
    {
        Assert.Equal(0, StockForms.ParseCount("0"));
        Assert.Null(StockForms.ParseCount("-1"));
        Assert.Equal("‎−2", StockForms.Signed(-2));
        Assert.Equal("0", StockForms.Signed(0));
    }
}
