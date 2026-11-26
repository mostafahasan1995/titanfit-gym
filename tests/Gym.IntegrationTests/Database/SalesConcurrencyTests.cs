namespace Gym.IntegrationTests.Database;

public sealed class SalesConcurrencyTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Two_tills_selling_at_the_same_time_never_oversell()
    {
        var productId = await fx.AddProductAsync(stock: 5);
        int[] cashiers = [await fx.AddStaffAsync(StaffType.Cashier), await fx.AddStaffAsync(StaffType.Cashier)];

        // 10 single-item sales at once from 2 tills, only 5 in stock.
        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(i =>
            TrySellAsync(productId, 1, cashiers[i % 2], i % 2 == 0 ? "Cafeteria 1" : "Cafeteria 2")));

        Assert.Equal(5, results.Count(sold => sold));
        Assert.Equal(0, await fx.StockOfAsync(productId));
    }

    [Fact]
    public async Task Last_items_go_to_one_till_only()
    {
        var productId = await fx.AddProductAsync(stock: 5);
        var cashier1 = await fx.AddStaffAsync(StaffType.Cashier);
        var cashier2 = await fx.AddStaffAsync(StaffType.Cashier);

        var results = await Task.WhenAll(
            TrySellAsync(productId, 3, cashier1, "Cafeteria 1"),
            TrySellAsync(productId, 3, cashier2, "Cafeteria 2"));

        Assert.Single(results, sold => sold);
        Assert.Equal(2, await fx.StockOfAsync(productId));
    }

    [Fact]
    public async Task One_cashier_on_two_tills_gets_one_shift()
    {
        var product1 = await fx.AddProductAsync(stock: 10);
        var product2 = await fx.AddProductAsync(stock: 10);
        var cashier = await fx.AddStaffAsync(StaffType.Cashier);

        var results = await Task.WhenAll(
            TrySellAsync(product1, 1, cashier, "Cafeteria 1"),
            TrySellAsync(product2, 1, cashier, "Cafeteria 2"));

        Assert.All(results, Assert.True);
        await using var db = fx.CreateContext();
        Assert.Equal(1, await db.Shifts.CountAsync(s => s.CashierId == cashier));
        Assert.Equal(2, await db.Sales.CountAsync(s => s.CashierId == cashier && s.ShiftId != null));
    }

    [Fact]
    public async Task Sale_saves_lines_and_stock_movements_together()
    {
        var water = await fx.AddProductAsync(stock: 10, price: 5_000);
        var shake = await fx.AddProductAsync(stock: 4, price: 25_000);
        var cashier = await fx.AddStaffAsync(StaffType.Cashier);

        await using var db = fx.CreateContext();
        var receipt = await fx.Sales(db).CreateAsync(
            new CreateSaleRequest("Cafeteria 1", [new(water, 2), new(shake, 1), new(water, 1)], CashReceived: 50_000), cashier);

        Assert.True(receipt.Number > 0);
        Assert.Equal(40_000, receipt.Total); // water 3 × 5,000 + shake 25,000
        Assert.Equal(10_000, receipt.Change);
        Assert.Equal(new[] { (water, 3), (shake, 1) }, receipt.Lines.OrderBy(l => l.ProductId).Select(l => (l.ProductId, l.Quantity)));
        Assert.Equal(7, await fx.StockOfAsync(water));
        Assert.Equal(3, await fx.StockOfAsync(shake));

        await using var check = fx.CreateContext();
        var movements = await check.StockMovements.Where(m => m.SaleId == receipt.Id).OrderBy(m => m.ProductId).ToListAsync();
        Assert.Equal(new[] { -3, -1 }, movements.Select(m => m.Quantity));
        Assert.All(movements, m => Assert.Equal(StockMovementType.Sale, m.Type));
    }

    [Fact]
    public async Task Failed_sale_leaves_nothing_behind()
    {
        var inStock = await fx.AddProductAsync(stock: 5);
        var soldOut = await fx.AddProductAsync(stock: 0);
        var cashier = await fx.AddStaffAsync(StaffType.Cashier);

        await using var db = fx.CreateContext();
        var error = await Assert.ThrowsAsync<InsufficientStockException>(() =>
            fx.Sales(db).CreateAsync(new CreateSaleRequest("Cafeteria 1", [new(inStock, 1), new(soldOut, 1)]), cashier));

        Assert.Equal(soldOut, Assert.Single(error.Shortages).ProductId);
        Assert.Equal(5, await fx.StockOfAsync(inStock));
        await using var check = fx.CreateContext();
        Assert.False(await check.Sales.AnyAsync(s => s.CashierId == cashier));
        Assert.False(await check.StockMovements.AnyAsync(m => m.ProductId == inStock && m.Type == StockMovementType.Sale));
    }

    [Fact]
    public async Task Adjustment_cannot_make_stock_negative()
    {
        var productId = await fx.AddProductAsync(stock: 3);
        var staff = await fx.AddStaffAsync(StaffType.Cashier);

        await using var db = fx.CreateContext();
        var stock = fx.Stock(db);
        var error = await Assert.ThrowsAsync<DomainException>(() => stock.AdjustAsync(new StockAdjustmentRequest(productId, Delta: -4), staff));
        Assert.Equal("negative_stock", error.Code);

        var counted = await stock.AdjustAsync(new StockAdjustmentRequest(productId, CountedQuantity: 1, Note: "Stocktake"), staff);
        Assert.Equal(1, counted.Stock);
        Assert.Equal(1, await fx.StockOfAsync(productId));
    }

    private async Task<bool> TrySellAsync(int productId, int quantity, int cashierId, string station)
    {
        await using var db = fx.CreateContext();
        try
        {
            await fx.Sales(db).CreateAsync(new CreateSaleRequest(station, [new SaleLineRequest(productId, quantity)]), cashierId);
            return true;
        }
        catch (InsufficientStockException)
        {
            return false;
        }
    }
}
