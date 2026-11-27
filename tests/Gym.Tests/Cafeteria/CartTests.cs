using Gym.Cafeteria.Sales;
using Gym.Contracts;
using Gym.UI;

namespace Gym.Tests.Cafeteria;

public sealed class CartTests
{
    internal static ProductDto Product(int id, long price, int stock, bool active = true, string? barcode = null) =>
        new(id, $"Item {id}", $"صنف {id}", 1, "Drinks", "مشروبات", barcode, price, 5, active, stock, stock < 5);

    [Fact]
    public void Adding_the_same_item_again_raises_the_quantity_and_the_totals_are_exact()
    {
        var cart = new Cart();
        var water = Product(1, 3_000, 10);
        var shake = Product(6, 35_000, 4);

        Assert.Equal(AddResult.Added, cart.Add(water));
        Assert.Equal(AddResult.Added, cart.Add(water));
        Assert.Equal(AddResult.Added, cart.Add(shake));

        Assert.Equal(2, cart.Lines.Count);
        Assert.Equal(2, cart.QuantityOf(1));
        Assert.Equal(6_000, cart.Find(1)!.LineTotal);
        Assert.Equal(41_000, cart.Total);
        Assert.Equal(3, cart.ItemCount);
    }

    [Fact]
    public void Large_amounts_add_up_without_rounding()
    {
        var cart = new Cart();
        var whey = Product(13, 450_000, 50);
        for (var i = 0; i < 7; i++)
        {
            cart.Add(whey);
        }

        cart.Add(Product(1, 3_000, 10));

        Assert.Equal(3_153_000, cart.Total);
    }

    [Fact]
    public void The_cart_never_holds_more_than_the_stock()
    {
        var cart = new Cart();
        var bar = Product(9, 20_000, 2);

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

        Assert.Equal(AddResult.OutOfStock, cart.Add(Product(5, 15_000, 0)));
        Assert.Equal(AddResult.Inactive, cart.Add(Product(17, 5_000, 10, active: false)));
        Assert.True(cart.IsEmpty);
        Assert.Equal(0, cart.Total);
    }

    [Fact]
    public void Minus_lowers_the_quantity_and_removes_the_line_at_zero()
    {
        var cart = new Cart();
        var water = Product(1, 3_000, 10);
        cart.Add(water);
        cart.Add(water);
        var line = cart.Find(1)!;

        cart.Decrease(line);
        Assert.Equal(1, line.Quantity);
        Assert.Equal(3_000, cart.Total);

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
        cart.Add(Product(1, 3_000, 10));
        cart.Add(Product(2, 6_000, 10));

        cart.Remove(cart.Find(1)!);
        Assert.Equal(6_000, cart.Total);

        cart.Clear();
        Assert.True(cart.IsEmpty);
        Assert.Contains(nameof(Cart.Total), changes);
        Assert.Contains(nameof(Cart.IsEmpty), changes);
    }

    [Fact]
    public void A_new_price_from_the_server_updates_the_line()
    {
        var cart = new Cart();
        cart.Add(Product(1, 3_000, 10));
        cart.Add(Product(1, 3_000, 10));

        cart.UpdateProduct(Product(1, 3_500, 10));

        Assert.Equal(7_000, cart.Total);
    }

    [Fact]
    public void Short_lines_are_marked_with_what_is_missing()
    {
        var cart = new Cart { Language = AppLanguage.English };
        var water = Product(2, 6_000, 8);
        for (var i = 0; i < 8; i++)
        {
            cart.Add(water);
        }

        cart.Add(Product(1, 3_000, 10));
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
        cart.Add(Product(1, 3_000, 10));
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
        cart.Add(Product(1, 3_000, 10));
        cart.Add(Product(1, 3_000, 10));
        cart.Add(Product(9, 20_000, 10));

        Assert.Equal(new[] { new SaleLineRequest(1, 2), new SaleLineRequest(9, 1) }, cart.ToSaleLines());
    }

    [Theory]
    [InlineData(41_000L, 50_000L, 9_000L)]
    [InlineData(41_000L, 41_000L, 0L)]
    [InlineData(41_000L, 40_000L, null)]
    [InlineData(41_000L, null, null)]
    public void The_change_is_the_cash_minus_the_total(long total, long? cash, long? change)
    {
        Assert.Equal(change, CashMath.ChangeDue(total, cash));
    }

    [Fact]
    public void Quick_cash_amounts_round_up_to_the_usual_notes()
    {
        Assert.Equal(new long[] { 51_000, 55_000, 60_000, 100_000 }, CashMath.QuickAmounts(51_000));
        Assert.Equal(new long[] { 3_000, 5_000, 10_000, 50_000 }, CashMath.QuickAmounts(3_000));
        Assert.Equal(new long[] { 50_000 }, CashMath.QuickAmounts(50_000));
        Assert.Empty(CashMath.QuickAmounts(0));
    }

    [Theory]
    [InlineData(1L, 5_000L, 5_000L)]
    [InlineData(5_000L, 5_000L, 5_000L)]
    [InlineData(5_001L, 5_000L, 10_000L)]
    [InlineData(449_999L, 50_000L, 450_000L)]
    [InlineData(0L, 5_000L, 0L)]
    public void Amounts_round_up_to_whole_steps(long amount, long step, long expected)
    {
        Assert.Equal(expected, CashMath.RoundUp(amount, step));
    }
}
