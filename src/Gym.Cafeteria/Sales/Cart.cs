using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Gym.Cafeteria.Receipts;
using Gym.Contracts;
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

    public long UnitPrice => Product.Price;

    /// <summary>Whole SYP.</summary>
    public long LineTotal => checked(Product.Price * Quantity);

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
/// The cashier's current order. Money is whole Syrian pounds, so the totals are exact sums (no rounding).
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

    /// <summary>Sum of the line totals, whole SYP.</summary>
    public long Total => Lines.Aggregate(0L, (sum, line) => checked(sum + line.LineTotal));

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

/// <summary>Cash helpers for the charge panel. Whole SYP.</summary>
public static class CashMath
{
    /// <summary>The change to give back, or null when no amount was typed or it is less than the total.</summary>
    public static long? ChangeDue(long total, long? cashReceived) =>
        cashReceived is { } cash && cash >= total ? cash - total : null;

    /// <summary>
    /// Quick amounts for the "cash received" buttons: the exact total, then the total rounded up to the next
    /// 5,000, 10,000 and 50,000 (the notes customers usually hand over). Distinct and ascending, at most 4.
    /// </summary>
    public static IReadOnlyList<long> QuickAmounts(long total)
    {
        if (total <= 0)
        {
            return [];
        }

        return new[] { total, RoundUp(total, 5_000), RoundUp(total, 10_000), RoundUp(total, 50_000) }
            .Distinct()
            .Order()
            .Take(4)
            .ToList();
    }

    /// <summary>The smallest multiple of <paramref name="step"/> that is at least <paramref name="amount"/>.</summary>
    public static long RoundUp(long amount, long step)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(step);
        if (amount <= 0)
        {
            return 0;
        }

        return checked((amount + step - 1) / step * step);
    }
}
