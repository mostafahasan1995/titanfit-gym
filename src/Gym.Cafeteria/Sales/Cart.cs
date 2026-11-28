using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Gym.Cafeteria.Receipts;
using Gym.Contracts;
using Gym.Domain;
using Gym.UI;

namespace Gym.Cafeteria.Sales;

/// <summary>What happened when an item was added to the cart.</summary>
public enum AddResult
{
    Added,

    /// <summary>Nothing in stock.</summary>
    OutOfStock,

    /// <summary>The cart already holds all of the stock.</summary>
    NoMoreStock,

    /// <summary>The item is not for sale (inactive).</summary>
    Inactive,
}

/// <summary>One cart line: a product and how many. The price is the product's current price (the server uses the same).</summary>
public sealed class CartLine : ObservableObject
{
    private ProductDto product;
    private int quantity;
    private int? shortBy;
    private AppLanguage language;

    public CartLine(ProductDto product, int quantity, AppLanguage language = AppLanguage.English)
    {
        ArgumentNullException.ThrowIfNull(product);
        this.product = product;
        this.quantity = quantity;
        this.language = language;
    }

    /// <summary>The language of <see cref="Name"/> and the other texts.</summary>
    public AppLanguage Language
    {
        get => language;
        internal set
        {
            if (SetProperty(ref language, value))
            {
                RaiseTexts();
            }
        }
    }

    public string Name => ReceiptLayout.ProductName(Product.NameEn, Product.NameAr, Language);

    /// <summary>"Only 3 in stock" after the server refused the sale, else empty.</summary>
    public string ShortText => ShortBy is { } missing && missing > 0
        ? CafeteriaText.OnlyInStock.Format(Language, NumberText.Format(Math.Max(0, Quantity - missing)))
        : "";

    public string DecreaseName => (Quantity <= 1 ? CafeteriaText.RemoveItem : CafeteriaText.RemoveOne).Format(Language, Name);

    public string IncreaseName => CafeteriaText.AddOne.Format(Language, Name);

    public ProductDto Product
    {
        get => product;
        internal set
        {
            if (SetProperty(ref product, value))
            {
                OnPropertyChanged(nameof(UnitPrice));
                OnPropertyChanged(nameof(LineTotal));
                OnPropertyChanged(nameof(CanIncrease));
                RaiseTexts();
            }
        }
    }

    public int ProductId => Product.Id;

    public int Quantity
    {
        get => quantity;
        internal set
        {
            if (SetProperty(ref quantity, value))
            {
                OnPropertyChanged(nameof(LineTotal));
                OnPropertyChanged(nameof(CanIncrease));
                RaiseTexts();
            }
        }
    }

    /// <summary>US dollars.</summary>
    public decimal UnitPrice => Product.Price;

    /// <summary>US dollars.</summary>
    public decimal LineTotal => Product.Price * Quantity;

    /// <summary>False when the cart already holds the whole stock of this item.</summary>
    public bool CanIncrease => Quantity < Product.Stock;

    /// <summary>Set after the server refused the sale: how many are missing (null = enough stock).</summary>
    public int? ShortBy
    {
        get => shortBy;
        internal set
        {
            if (SetProperty(ref shortBy, value))
            {
                OnPropertyChanged(nameof(IsShort));
                OnPropertyChanged(nameof(ShortText));
            }
        }
    }

    public bool IsShort => ShortBy is > 0;

    private void RaiseTexts()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(ShortText));
        OnPropertyChanged(nameof(DecreaseName));
        OnPropertyChanged(nameof(IncreaseName));
    }
}

/// <summary>
/// The cashier's current order. Money is US dollars with cents (decimal), so the totals are exact sums.
/// The cart never asks for more than the stock it knows; the server checks again when the sale is saved.
/// </summary>
public sealed class Cart : ObservableObject
{
    private AppLanguage language;

    public Cart()
    {
        Lines.CollectionChanged += OnLinesChanged;
    }

    /// <summary>The language of the line texts.</summary>
    public AppLanguage Language
    {
        get => language;
        set
        {
            if (SetProperty(ref language, value))
            {
                foreach (var line in Lines)
                {
                    line.Language = value;
                }
            }
        }
    }

    public ObservableCollection<CartLine> Lines { get; } = [];

    /// <summary>Sum of the line totals, US dollars.</summary>
    public decimal Total => Lines.Sum(line => line.LineTotal);

    /// <summary>Number of pieces (2 waters + 1 bar = 3).</summary>
    public int ItemCount => Lines.Sum(l => l.Quantity);

    public bool IsEmpty => Lines.Count == 0;

    public bool HasShortage => Lines.Any(l => l.IsShort);

    public int QuantityOf(int productId) => Find(productId)?.Quantity ?? 0;

    public CartLine? Find(int productId) => Lines.FirstOrDefault(l => l.ProductId == productId);

    /// <summary>Adds one piece of <paramref name="product"/> (a tile tap or a barcode scan).</summary>
    public AddResult Add(ProductDto product)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (!product.IsActive)
        {
            return AddResult.Inactive;
        }

        var line = Find(product.Id);
        if (line is null)
        {
            if (product.Stock <= 0)
            {
                return AddResult.OutOfStock;
            }

            Lines.Add(new CartLine(product, 1, language));
            return AddResult.Added;
        }

        line.Product = product;
        if (line.Quantity >= product.Stock)
        {
            return product.Stock <= 0 ? AddResult.OutOfStock : AddResult.NoMoreStock;
        }

        line.Quantity++;
        line.ShortBy = null;
        return AddResult.Added;
    }

    /// <summary>The + button. False when the stock is used up.</summary>
    public bool Increase(CartLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        return Add(line.Product) == AddResult.Added;
    }

    /// <summary>The − button: one less, and the line goes away at zero.</summary>
    public void Decrease(CartLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        if (line.Quantity <= 1)
        {
            Lines.Remove(line);
            return;
        }

        line.Quantity--;
        if (line.ShortBy is { } shortBy)
        {
            line.ShortBy = shortBy > 1 ? shortBy - 1 : null;
        }
    }

    public void Remove(CartLine line)
    {
        ArgumentNullException.ThrowIfNull(line);
        Lines.Remove(line);
    }

    public void Clear()
    {
        // Clear() raises Reset without the old items: unhook the lines first.
        foreach (var line in Lines)
        {
            line.PropertyChanged -= OnLineChanged;
        }

        Lines.Clear();
    }

    /// <summary>New price, name or stock of a product from the server (live update or reload).</summary>
    public void UpdateProduct(ProductDto product)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (Find(product.Id) is { } line)
        {
            line.Product = product;
            Raise();
        }
    }

    /// <summary>Marks the lines the server could not sell: <paramref name="available"/> is the stock per product.</summary>
    public void MarkShortages(IReadOnlyDictionary<int, int> available)
    {
        ArgumentNullException.ThrowIfNull(available);
        foreach (var line in Lines)
        {
            line.ShortBy = available.TryGetValue(line.ProductId, out var stock) && line.Quantity > stock ? line.Quantity - stock : null;
        }

        OnPropertyChanged(nameof(HasShortage));
    }

    /// <summary>The lines for <c>POST /api/sales</c>.</summary>
    public IReadOnlyList<SaleLineRequest> ToSaleLines() => Lines.Select(l => new SaleLineRequest(l.ProductId, l.Quantity)).ToList();

    private void OnLinesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (CartLine line in e.OldItems)
            {
                line.PropertyChanged -= OnLineChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (CartLine line in e.NewItems)
            {
                line.PropertyChanged += OnLineChanged;
            }
        }

        Raise();
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void OnLineChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(CartLine.Quantity) or nameof(CartLine.Product) or nameof(CartLine.ShortBy))
        {
            Raise();
        }
    }

    private void Raise()
    {
        OnPropertyChanged(nameof(Total));
        OnPropertyChanged(nameof(ItemCount));
        OnPropertyChanged(nameof(HasShortage));
    }
}

/// <summary>
/// Cash helpers for the charge panel. Amounts are in the currency the customer pays in: US dollars (cents) or
/// whole Syrian pounds.
/// </summary>
public static class CashMath
{
    /// <summary>The change to give back, or null when no amount was typed or it is less than the amount due.</summary>
    public static decimal? ChangeDue(decimal due, decimal? cashReceived) =>
        cashReceived is { } cash && cash >= due ? cash - due : null;

    /// <summary>
    /// Quick amounts for the "cash received" buttons, distinct and ascending, at most 4. Pounds: the exact amount,
    /// then rounded up to the next 5,000, 10,000 and 50,000 (the notes customers usually hand over). Dollars: the
    /// exact amount, then the next $1, $5, $10 and $20.
    /// </summary>
    public static IReadOnlyList<decimal> QuickAmounts(decimal due, Currency currency = Currency.Usd)
    {
        if (due <= 0)
        {
            return [];
        }

        var steps = currency == Currency.Syp ? new[] { 5_000m, 10_000m, 50_000m } : new[] { 1m, 5m, 10m, 20m };
        return new[] { due }.Concat(steps.Select(step => RoundUp(due, step)))
            .Distinct()
            .Order()
            .Take(4)
            .ToList();
    }

    /// <summary>The smallest multiple of <paramref name="step"/> that is at least <paramref name="amount"/>.</summary>
    public static decimal RoundUp(decimal amount, decimal step)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step);
        if (amount <= 0)
        {
            return 0;
        }

        return Math.Ceiling(amount / step) * step;
    }
}

/// <summary>A quick cash button: the amount in the paid currency and its text ("20.00" or "150,000").</summary>
public sealed record QuickCash(decimal Amount, Currency Currency)
{
    public string Text => Money.FormatAmount(Currency, Amount);
}
