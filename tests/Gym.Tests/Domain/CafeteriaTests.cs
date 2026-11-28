using Gym.Domain;

namespace Gym.Tests.Domain;

public sealed class CafeteriaTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(4, 5, true)]
    [InlineData(5, 5, false)] // at the minimum is not low
    [InlineData(6, 5, false)]
    [InlineData(0, 0, false)]
    public void Low_stock_means_below_the_minimum(int stock, int min, bool low)
    {
        Assert.Equal(low, StockRules.IsLow(stock, min));
    }

    [Fact]
    public void Sale_line_above_the_stock_is_refused_with_every_shortage()
    {
        var water = new Product { Id = 1, NameEn = "Water" };
        var shake = new Product { Id = 2, NameEn = "Protein shake" };
        var bar = new Product { Id = 3, NameEn = "Bar" };

        var error = Assert.Throws<InsufficientStockException>(() =>
            StockRules.EnsureAvailable([(water, 5, 5), (shake, 1, 2), (bar, 0, 1)]));

        Assert.Equal("insufficient_stock", error.Code);
        Assert.Equal(new[] { new StockShortage(2, "Protein shake", 1, 2), new StockShortage(3, "Bar", 0, 1) }, error.Shortages);
        Assert.NotNull(error.Details);
    }

    [Fact]
    public void Selling_exactly_the_stock_is_allowed()
    {
        StockRules.EnsureAvailable([(new Product { Id = 1 }, 3, 3)]);
    }

    [Theory]
    [InlineData(10, -4, null, -4)]
    [InlineData(10, 5, null, 5)]
    [InlineData(10, null, 7, -3)] // stocktake: counted 7
    [InlineData(10, null, 10, 0)]
    [InlineData(0, null, 12, 12)]
    public void Adjustment_by_change_or_by_count(int stock, int? delta, int? counted, int expected)
    {
        Assert.Equal(expected, StockRules.AdjustmentDelta(stock, delta, counted));
    }

    [Fact]
    public void Adjustment_cannot_make_the_stock_negative()
    {
        Assert.Equal("negative_stock", Assert.Throws<DomainException>(() => StockRules.AdjustmentDelta(3, -4, null)).Code);
        Assert.Equal("negative_stock", Assert.Throws<DomainException>(() => StockRules.AdjustmentDelta(3, null, -1)).Code);
    }

    [Fact]
    public void Adjustment_needs_exactly_one_of_change_or_count()
    {
        Assert.Equal("invalid_adjustment", Assert.Throws<DomainException>(() => StockRules.AdjustmentDelta(3, null, null)).Code);
        Assert.Equal("invalid_adjustment", Assert.Throws<DomainException>(() => StockRules.AdjustmentDelta(3, 1, 4)).Code);
    }

    [Fact]
    public void Sale_total_is_the_sum_of_the_lines_at_the_current_price()
    {
        var sale = new Sale();

        sale.AddLine(new Product { Id = 1, Price = 1.25m }, 3);
        sale.AddLine(new Product { Id = 2, Price = 2.50m }, 1);
        sale.SetCashReceived(10m);

        Assert.Equal(6.25m, sale.Total);
        Assert.Equal(new[] { 3.75m, 2.50m }, sale.Lines.Select(l => l.LineTotal));
        Assert.Equal(3.75m, sale.Change);
        Assert.Equal((Currency.Usd, 6.25m), (sale.Currency, sale.PaidAmount));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(Sale.MaxLineQuantity + 1)]
    public void Sale_line_quantity_must_be_positive(int quantity)
    {
        Assert.Equal("invalid_quantity", Assert.Throws<DomainException>(() => new Sale().AddLine(new Product(), quantity)).Code);
    }

    [Fact]
    public void Cash_received_cannot_be_below_the_total()
    {
        var sale = new Sale();
        sale.AddLine(new Product { Price = 10m }, 1);

        Assert.Equal("cash_received_too_low", Assert.Throws<DomainException>(() => sale.SetCashReceived(9.99m)).Code);
        sale.SetCashReceived(null);
        Assert.Null(sale.Change);
    }

    [Fact]
    public void Shift_closes_once()
    {
        var shift = new Shift { OpenedAt = Now };

        shift.Close(120.50m, " ok ", Now.AddHours(8));

        Assert.False(shift.IsOpen);
        Assert.Equal(120.50m, shift.ClosingCash);
        Assert.Equal("ok", shift.Note);
        Assert.Equal("shift_closed", Assert.Throws<ConflictException>(() => shift.Close(null, null, Now.AddHours(9))).Code);
    }

    [Fact]
    public void Default_categories_have_english_and_arabic_names()
    {
        Assert.Equal(new[] { "Drinks", "Shakes", "Snacks", "Supplements" }, ProductCategory.Defaults().Select(c => c.NameEn));
        Assert.All(ProductCategory.Defaults(), c => Assert.False(string.IsNullOrWhiteSpace(c.NameAr)));
    }
}
