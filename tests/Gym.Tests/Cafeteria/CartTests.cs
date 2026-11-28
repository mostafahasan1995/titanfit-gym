using Gym.Cafeteria.Sales;
using Gym.Contracts;
using Gym.Domain;
using Gym.UI;

namespace Gym.Tests.Cafeteria;

public sealed class CartTests
{
    internal static ProductDto Product(int id, decimal price, int stock, bool active = true, string? barcode = null) =>
        new(id, $"Item {id}", $"صنف {id}", 1, "Drinks", "مشروبات", barcode, price, 5, active, stock, stock < 5);

    [Fact]
    public void Adding_the_same_item_again_raises_the_quantity_and_the_totals_are_exact()
    {
        var cart = new Cart();
        var water = Product(1, 0.25m, 10);
        var shake = Product(6, 3.00m, 4);

        Assert.Equal(AddResult.Added, cart.Add(water));
        Assert.Equal(AddResult.Added, cart.Add(water));
        Assert.Equal(AddResult.Added, cart.Add(shake));

        Assert.Equal(2, cart.Lines.Count);
        Assert.Equal(2, cart.QuantityOf(1));
        Assert.Equal(0.50m, cart.Find(1)!.LineTotal);
        Assert.Equal(3.50m, cart.Total);
        Assert.Equal(3, cart.ItemCount);
    }

    [Fact]
    public void Cents_add_up_without_rounding()
    {
        var cart = new Cart();
        var whey = Product(13, 38.00m, 50);
        for (var i = 0; i < 7; i++)
        {
            cart.Add(whey);
        }

        cart.Add(Product(1, 0.25m, 10));

        Assert.Equal(266.25m, cart.Total);
    }

    [Fact]
    public void The_cart_never_holds_more_than_the_stock()
    {
        var cart = new Cart();
        var bar = Product(9, 1.75m, 2);

        Assert.Equal(AddResult.Added, cart.Add(bar));
        Assert.True(cart.Find(9)!.CanIncrease);
        Assert.True(cart.Increase(cart.Find(9)!));
        Assert.False(cart.Find(9)!.CanIncrease);
        Assert.False(cart.Increase(cart.Find(9)!));
        Assert.Equal(AddResult.NoMoreStock, cart.Add(bar));
        Assert.Equal(2, cart.QuantityOf(9));
    }

    [Fact]
    public void Out_of_stock_and_inactive_items_are_refused()
    {
        var cart = new Cart();

        Assert.Equal(AddResult.OutOfStock, cart.Add(Product(5, 1.25m, 0)));
        Assert.Equal(AddResult.Inactive, cart.Add(Product(17, 0.40m, 10, active: false)));
        Assert.True(cart.IsEmpty);
        Assert.Equal(0m, cart.Total);
    }

    [Fact]
    public void Minus_lowers_the_quantity_and_removes_the_line_at_zero()
    {
        var cart = new Cart();
        var water = Product(1, 0.25m, 10);
        cart.Add(water);
        cart.Add(water);
        var line = cart.Find(1)!;

        cart.Decrease(line);
        Assert.Equal(1, line.Quantity);
        Assert.Equal(0.25m, cart.Total);

        cart.Decrease(line);
        Assert.True(cart.IsEmpty);
        Assert.Equal(0, cart.ItemCount);
    }

    [Fact]
    public void Remove_and_clear_empty_the_cart_and_notify()
    {
        var cart = new Cart();
        var changes = new List<string?>();
        cart.PropertyChanged += (_, e) => changes.Add(e.PropertyName);
        cart.Add(Product(1, 0.25m, 10));
        cart.Add(Product(2, 0.50m, 10));

        cart.Remove(cart.Find(1)!);
        Assert.Equal(0.50m, cart.Total);

        cart.Clear();
        Assert.True(cart.IsEmpty);
        Assert.Contains(nameof(Cart.Total), changes);
        Assert.Contains(nameof(Cart.IsEmpty), changes);
    }

    [Fact]
    public void A_new_price_from_the_server_updates_the_line()
    {
        var cart = new Cart();
        cart.Add(Product(1, 0.25m, 10));
        cart.Add(Product(1, 0.25m, 10));

        cart.UpdateProduct(Product(1, 0.30m, 10));

        Assert.Equal(0.60m, cart.Total);
    }

    [Fact]
    public void Short_lines_are_marked_with_what_is_missing()
    {
        var cart = new Cart { Language = AppLanguage.English };
        var water = Product(2, 0.50m, 8);
        for (var i = 0; i < 8; i++)
        {
            cart.Add(water);
        }

        cart.Add(Product(1, 0.25m, 10));
        cart.MarkShortages(new Dictionary<int, int> { [2] = 7 });

        Assert.True(cart.HasShortage);
        Assert.Equal(1, cart.Find(2)!.ShortBy);
        Assert.Equal("Only 7 in stock", cart.Find(2)!.ShortText);
        Assert.False(cart.Find(1)!.IsShort);

        cart.Decrease(cart.Find(2)!);
        Assert.False(cart.HasShortage);
    }

    [Fact]
    public void Line_texts_follow_the_language()
    {
        var cart = new Cart();
        cart.Add(Product(1, 0.25m, 10));
        var line = cart.Find(1)!;
        Assert.Equal("Item 1", line.Name);
        Assert.Equal("Remove Item 1", line.DecreaseName);

        cart.Language = AppLanguage.Arabic;

        Assert.Equal("صنف 1", line.Name);
        Assert.Contains("صنف 1", line.IncreaseName, StringComparison.Ordinal);
    }

    [Fact]
    public void The_sale_lines_hold_each_item_once_with_its_quantity()
    {
        var cart = new Cart();
        cart.Add(Product(1, 0.25m, 10));
        cart.Add(Product(1, 0.25m, 10));
        cart.Add(Product(9, 1.75m, 10));

        Assert.Equal(new[] { new SaleLineRequest(1, 2), new SaleLineRequest(9, 1) }, cart.ToSaleLines());
    }

    [Theory]
    [InlineData("3.50", "5", "1.50")]
    [InlineData("3.50", "3.50", "0")]
    [InlineData("3.50", "3", null)]
    [InlineData("3.50", null, null)]
    [InlineData("40250", "50000", "9750")]
    public void The_change_is_the_cash_minus_the_amount_due(string due, string? cash, string? change)
    {
        Assert.Equal(D(change), CashMath.ChangeDue(D(due)!.Value, D(cash)));
    }

    [Fact]
    public void Quick_cash_in_dollars_rounds_up_to_the_usual_notes()
    {
        Assert.Equal(new[] { 3.50m, 4m, 5m, 10m }, CashMath.QuickAmounts(3.50m, Currency.Usd));
        Assert.Equal(new[] { 12.25m, 13m, 15m, 20m }, CashMath.QuickAmounts(12.25m));
        Assert.Equal(new[] { 20m }, CashMath.QuickAmounts(20m));
        Assert.Empty(CashMath.QuickAmounts(0m));
    }

    [Fact]
    public void Quick_cash_in_pounds_rounds_up_to_the_usual_notes()
    {
        Assert.Equal(new[] { 51_000m, 55_000m, 60_000m, 100_000m }, CashMath.QuickAmounts(51_000m, Currency.Syp));
        Assert.Equal(new[] { 3_000m, 5_000m, 10_000m, 50_000m }, CashMath.QuickAmounts(3_000m, Currency.Syp));
        Assert.Equal(new[] { 50_000m }, CashMath.QuickAmounts(50_000m, Currency.Syp));
    }

    [Fact]
    public void A_quick_cash_button_shows_the_amount_in_its_currency()
    {
        Assert.Equal("20.00", new QuickCash(20m, Currency.Usd).Text);
        Assert.Equal("150,000", new QuickCash(150_000m, Currency.Syp).Text);
    }

    [Theory]
    [InlineData("1", "5000", "5000")]
    [InlineData("5000", "5000", "5000")]
    [InlineData("5001", "5000", "10000")]
    [InlineData("449999", "50000", "450000")]
    [InlineData("0", "5000", "0")]
    [InlineData("3.01", "1", "4")]
    [InlineData("12.25", "5", "15")]
    public void Amounts_round_up_to_whole_steps(string amount, string step, string expected)
    {
        Assert.Equal(D(expected), CashMath.RoundUp(D(amount)!.Value, D(step)!.Value));
    }

    private static decimal? D(string? text) => text is null ? null : decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
}
