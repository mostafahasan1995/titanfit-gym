using System.Net;
using Gym.Client;

namespace Gym.IntegrationTests.Api;

public sealed class CafeteriaFlowTests(ApiFactory fx) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Sale_flow_from_stock_in_to_shift_close()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var cashier = await fx.CashierAsync();
        var shake = await cashier.NewProductAsync(price: 2.50m, minStock: 2);
        Assert.Equal(0, shake.Stock);
        Assert.True(shake.IsLowStock); // 0 is below the minimum of 2

        var level = await cashier.StockInAsync(new StockInRequest(shake.Id, 10, "Delivery"));
        Assert.Equal(10, level.Stock);
        Assert.Equal(shake.Id, (await cashier.FindProductByBarcodeAsync(shake.Barcode!))?.Id);
        Assert.Null(await cashier.FindProductByBarcodeAsync("0000000000000"));

        var shift = await cashier.OpenShiftAsync(new OpenShiftRequest("Cafeteria 1", OpeningCash: 20m));
        Assert.Equal(shift.Id, (await cashier.OpenShiftAsync(new OpenShiftRequest("Cafeteria 1"))).Id); // already open
        Assert.Equal(shift.Id, (await cashier.GetCurrentShiftAsync())?.Id);

        var receipt = await cashier.CreateSaleAsync(new CreateSaleRequest("Cafeteria 1", [new(shake.Id, 3)], CashReceived: 10m));
        Assert.Equal(7.50m, receipt.Total);
        Assert.Equal(2.50m, receipt.Change);
        Assert.Equal((Currency.Usd, 7.50m), (receipt.Currency, receipt.PaidAmount));
        Assert.Equal(shift.Id, receipt.ShiftId);
        Assert.Equal("صنف", Assert.Single(receipt.Lines).NameAr);
        Assert.Equal(receipt.Number, (await cashier.GetSaleAsync(receipt.Id)).Number);
        Assert.Equal(7, (await cashier.GetProductAsync(shake.Id)).Stock);

        var tooMany = await Assert.ThrowsAsync<GymApiException>(() =>
            cashier.CreateSaleAsync(new CreateSaleRequest("Cafeteria 1", [new(shake.Id, 8)])));
        Assert.Equal((HttpStatusCode.BadRequest, "insufficient_stock"), (tooMany.StatusCode, tooMany.Code));
        Assert.Contains("shortages", tooMany.Body, StringComparison.Ordinal);
        Assert.Equal(7, (await cashier.GetProductAsync(shake.Id)).Stock);

        var summary = await cashier.CloseShiftAsync(shift.Id, new CloseShiftRequest(ClosingCash: 26.50m, Note: "Short $1"));
        Assert.Equal(1, summary.SaleCount);
        Assert.Equal(7.50m, summary.SalesTotal);
        Assert.Equal(27.50m, summary.ExpectedCash);
        Assert.Equal(-1.00m, summary.CashDifference);
        Assert.Equal(new CurrencyTotals(7.50m, 0m, 0m, 1, 0), summary.Paid);
        Assert.Equal(3, Assert.Single(summary.Products).Quantity);
        Assert.NotNull(summary.Shift.ClosedAt);
        Assert.Null(await cashier.GetCurrentShiftAsync());

        var closedAgain = await Assert.ThrowsAsync<GymApiException>(() => cashier.CloseShiftAsync(shift.Id, new CloseShiftRequest()));
        Assert.Equal("shift_closed", closedAgain.Code);
        Assert.Contains(await cashier.GetSalesAsync(), s => s.Id == receipt.Id);
    }

    [Fact]
    public async Task Selling_without_an_open_shift_opens_one()
    {
        var cashier = await fx.CashierAsync();
        var water = await cashier.NewProductAsync(price: 0.50m, stock: 5);

        var receipt = await cashier.CreateSaleAsync(new CreateSaleRequest("Cafeteria 2", [new(water.Id, 1)]));

        var current = await cashier.GetCurrentShiftAsync();
        Assert.NotNull(current);
        Assert.Equal(current.Id, receipt.ShiftId);
        Assert.Equal("Cafeteria 2", current.Station);
    }

    [Fact]
    public async Task Stock_adjustments_and_low_stock()
    {
        var cashier = await fx.CashierAsync();
        var bar = await cashier.NewProductAsync(stock: 10, minStock: 5);

        var counted = await cashier.AdjustStockAsync(new StockAdjustmentRequest(bar.Id, CountedQuantity: 4, Note: "Stocktake"));
        Assert.Equal((4, true), (counted.Stock, counted.IsLow));
        Assert.Contains(await cashier.GetStockAsync(lowOnly: true), l => l.ProductId == bar.Id);

        var negative = await Assert.ThrowsAsync<GymApiException>(() => cashier.AdjustStockAsync(new StockAdjustmentRequest(bar.Id, Delta: -5)));
        Assert.Equal("negative_stock", negative.Code);

        var back = await cashier.AdjustStockAsync(new StockAdjustmentRequest(bar.Id, Delta: 6));
        Assert.Equal((10, false), (back.Stock, back.IsLow));
        Assert.DoesNotContain(await cashier.GetStockAsync(lowOnly: true), l => l.ProductId == bar.Id);

        var movements = await cashier.GetStockMovementsAsync(bar.Id);
        Assert.Equal(new[] { 6, -6, 10 }, movements.Select(m => m.Quantity));
    }

    [Fact]
    public async Task Cashier_cannot_close_another_cashiers_shift_but_admin_can()
    {
        var first = await fx.CashierAsync();
        var second = await fx.CashierAsync();
        var shift = await first.OpenShiftAsync(new OpenShiftRequest("Cafeteria 1"));

        var error = await Assert.ThrowsAsync<GymApiException>(() => second.CloseShiftAsync(shift.Id, new CloseShiftRequest()));
        Assert.Equal((HttpStatusCode.Forbidden, "not_your_shift"), (error.StatusCode, error.Code));

        var admin = await fx.AdminAsync();
        Assert.NotNull((await admin.CloseShiftAsync(shift.Id, new CloseShiftRequest())).Shift.ClosedAt);
    }

    [Fact]
    public async Task Products_and_categories_can_be_edited()
    {
        var cashier = await fx.CashierAsync();
        var category = await cashier.CreateCategoryAsync(new SaveCategoryRequest("Coffee", "قهوة", SortOrder: 5));
        var product = await cashier.CreateProductAsync(new SaveProductRequest("Espresso", "إسبريسو", category.Id, 1.75m));

        var updated = await cashier.UpdateProductAsync(product.Id, new SaveProductRequest("Espresso", "إسبريسو", category.Id, 2.005m, Barcode: " 6291234567890 ", IsActive: false));

        Assert.Equal((2.01m, "6291234567890", false), (updated.Price, updated.Barcode, updated.IsActive)); // cents kept
        Assert.DoesNotContain(await cashier.GetProductsAsync(), p => p.Id == product.Id);
        Assert.Contains(await cashier.GetProductsAsync(includeInactive: true), p => p.Id == product.Id);
        Assert.Contains(await cashier.GetProductsAsync(query: "espr", includeInactive: true), p => p.Id == product.Id);

        await cashier.StockInAsync(new StockInRequest(product.Id, 3));
        var inactive = await Assert.ThrowsAsync<GymApiException>(() => cashier.CreateSaleAsync(new CreateSaleRequest("Cafeteria 1", [new(product.Id, 1)])));
        Assert.Equal("product_inactive", inactive.Code);

        var duplicate = await Assert.ThrowsAsync<GymApiException>(() =>
            cashier.CreateProductAsync(new SaveProductRequest("Copy", "نسخة", category.Id, 1, Barcode: "6291234567890")));
        Assert.Equal((HttpStatusCode.Conflict, "duplicate_barcode"), (duplicate.StatusCode, duplicate.Code));
    }
}
