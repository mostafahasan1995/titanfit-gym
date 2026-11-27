using Gym.Cafeteria.Sales;
using Gym.Client;
using Gym.Client.Sample;
using Gym.Contracts;
using Gym.UI;

namespace Gym.Tests.Cafeteria;

public sealed class CashierViewModelTests
{
    private static async Task<(SampleGymApi Api, CashierViewModel Model)> StartAsync(AppLanguage language = AppLanguage.English)
    {
        var api = new SampleGymApi();
        await api.LoginAsync("ahmad", SampleGymApi.SamplePassword);
        var model = new CashierViewModel(api, () => "Cafeteria 2", language);
        await model.LoadAsync();
        return (api, model);
    }

    private static ProductDto P(CashierViewModel model, int id) => model.Products.Single(p => p.Id == id);

    [Fact]
    public async Task Loading_shows_the_active_items_the_category_tabs_and_the_open_shift()
    {
        var (_, model) = await StartAsync();

        Assert.Equal(16, model.Tiles.Count);
        Assert.DoesNotContain(model.Tiles, t => !t.Product.IsActive);
        Assert.Equal(new[] { "All", "Drinks", "Shakes", "Snacks", "Supplements" }, model.Categories.Select(c => c.Name));
        Assert.Equal(16, model.Categories[0].Count);
        Assert.True(model.Categories[0].IsSelected);
        Assert.NotNull(model.Shift);
        Assert.Equal(14, model.Shift.SaleCount);
        Assert.Contains(model.LowStock, p => p.Id == 5);
        Assert.All(model.LowStock, p => Assert.True(p.IsLowStock));
    }

    [Fact]
    public async Task Category_and_search_filter_the_tiles()
    {
        var (_, model) = await StartAsync();

        model.CategoryId = 4;
        Assert.Equal(4, model.Tiles.Count);
        Assert.True(model.Categories.Single(c => c.Id == 4).IsSelected);

        model.CategoryId = null;
        model.Search = "شيك";
        Assert.Equal(new[] { 6, 7, 8 }, model.Tiles.Select(t => t.Id).Order());

        model.Search = "5060495110124";
        Assert.Equal(9, Assert.Single(model.Tiles).Id);
    }

    [Fact]
    public async Task Tiles_show_the_stock_left_after_the_cart_and_the_in_cart_count()
    {
        var (_, model) = await StartAsync();
        var tile = model.Tiles.Single(t => t.Id == 14);
        Assert.Equal(1, tile.Left);
        Assert.Equal(StockState.Low, tile.State);
        Assert.Equal("Low stock: 1 left", tile.StockText);

        model.Add(tile.Product);

        Assert.Equal(1, tile.InCart);
        Assert.Equal("×1", tile.InCartText);
        Assert.Equal(0, tile.Left);
        Assert.Equal(StockState.Out, tile.State);
        Assert.False(tile.CanAdd);
        Assert.Equal("Out of stock", tile.StockText);

        model.Language = AppLanguage.Arabic;
        Assert.Equal("كرياتين 300 غ", tile.Name);
        Assert.Equal("نفد من المخزون", tile.StockText);
    }

    [Fact]
    public async Task A_scanned_barcode_adds_the_item_and_an_unknown_one_says_so()
    {
        var (_, model) = await StartAsync();

        var water = await model.ScanAsync("6291001000011");
        Assert.Equal(AddResult.Added, water.Result);
        Assert.Equal(1, model.Cart.QuantityOf(1));

        var unknown = await model.ScanAsync("0000000000000");
        Assert.True(unknown.IsUnknown);

        var empty = await model.ScanAsync("6291001000059");
        Assert.Equal(AddResult.OutOfStock, empty.Result);

        var inactive = await model.ScanAsync("6291001000172");
        Assert.Equal(AddResult.Inactive, inactive.Result);
        Assert.Single(model.Cart.Lines);
    }

    [Fact]
    public async Task Charge_sells_the_cart_with_the_station_and_shows_the_new_stock_and_shift_totals()
    {
        var (api, model) = await StartAsync();
        var waterStock = P(model, 1).Stock;
        var before = model.Shift!.SalesTotal;
        model.Add(P(model, 1));
        model.Add(P(model, 1));
        model.Add(P(model, 9));

        var outcome = await model.ChargeAsync(30_000);

        Assert.True(outcome.IsPaid);
        var receipt = outcome.Receipt!;
        Assert.Equal(26_000, receipt.Total);
        Assert.Equal(30_000, receipt.CashReceived);
        Assert.Equal(4_000, receipt.Change);
        Assert.Equal("Cafeteria 2", receipt.Station);
        Assert.Equal(receipt, model.LastReceipt);
        Assert.True(model.Cart.IsEmpty);
        Assert.Equal(waterStock - 2, P(model, 1).Stock);
        Assert.Equal(waterStock - 2, (await api.GetProductAsync(1)).Stock);
        Assert.Equal(15, model.Shift!.SaleCount);
        Assert.Equal(before + 26_000, model.Shift.SalesTotal);
    }

    [Fact]
    public async Task A_sale_with_no_open_shift_opens_one()
    {
        var (api, model) = await StartAsync();
        await api.CloseShiftAsync(model.Shift!.Shift.Id, new CloseShiftRequest());
        await model.RefreshShiftAsync();
        Assert.Null(model.Shift);

        model.Add(P(model, 1));
        var outcome = await model.ChargeAsync(null);

        Assert.True(outcome.IsPaid);
        Assert.NotNull(model.Shift);
        Assert.Equal(1, model.Shift.SaleCount);
        Assert.Null(outcome.Receipt!.Change);
    }

    [Fact]
    public async Task When_another_till_sold_the_stock_the_short_items_are_listed()
    {
        var (api, model) = await StartAsync();
        var water = P(model, 2);
        for (var i = 0; i < water.Stock; i++)
        {
            Assert.Equal(AddResult.Added, model.Add(water));
        }

        await api.CreateSaleAsync(new CreateSaleRequest("Cafeteria 1", [new SaleLineRequest(2, 1)]));
        var outcome = await model.ChargeAsync(null);

        Assert.False(outcome.IsPaid);
        Assert.Equal("insufficient_stock", Assert.IsType<GymApiException>(outcome.Error).Code);
        var shortage = Assert.Single(outcome.Shortages);
        Assert.Equal((2, 8, 7), (shortage.ProductId, shortage.InCart, shortage.Available));
        Assert.True(model.Cart.HasShortage);
        Assert.Equal(1, model.Cart.Find(2)!.ShortBy);
        Assert.Equal(7, P(model, 2).Stock);
    }

    [Fact]
    public void The_shortages_are_read_from_the_server_answer()
    {
        const string Body = """
            {"type":"about:blank","title":"Not enough stock","status":400,"code":"insufficient_stock",
             "shortages":[{"productId":2,"productName":"Water 1.5 L","available":3,"requested":5},{"ProductId":9,"Available":-1,"Requested":2}]}
            """;

        var shortages = CashierViewModel.ReadShortages(Body);

        Assert.Equal(3, shortages[2]);
        Assert.Equal(0, shortages[9]);
        Assert.Empty(CashierViewModel.ReadShortages("not json"));
        Assert.Empty(CashierViewModel.ReadShortages(null));
    }

    [Fact]
    public async Task Live_stock_updates_change_the_tiles_and_the_low_stock_list()
    {
        var (_, model) = await StartAsync();
        Assert.DoesNotContain(model.LowStock, p => p.Id == 1);

        model.ApplyStock([new StockLevelDto(1, "Water 500 ml", "مياه 500 مل", 3, 24, true)]);

        Assert.Equal(3, model.Tiles.Single(t => t.Id == 1).Left);
        Assert.Contains(model.LowStock, p => p.Id == 1);
    }

    [Fact]
    public async Task A_product_made_inactive_leaves_the_grid()
    {
        var (_, model) = await StartAsync();
        var bar = P(model, 9);

        model.ApplyProduct(bar with { IsActive = false });

        Assert.DoesNotContain(model.Tiles, t => t.Id == 9);
        Assert.Equal(15, model.Categories[0].Count);

        model.ApplyProduct(bar with { Price = 22_000 });
        Assert.Equal(22_000, model.Tiles.Single(t => t.Id == 9).Price);
    }
}
