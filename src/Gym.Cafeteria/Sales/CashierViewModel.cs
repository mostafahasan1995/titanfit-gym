using System.Collections.ObjectModel;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Gym.Client;
using Gym.Contracts;
using Gym.Domain;
using Gym.UI;

namespace Gym.Cafeteria.Sales;

public enum StockState
{
    Ok,
    Low,
    Out,
}

/// <summary>A product tile on the cashier grid: name, price, what is left after the cart, and the in-cart count.</summary>
public sealed class ProductTile : ObservableObject
{
    private ProductDto product;
    private int inCart;
    private AppLanguage language;

    public ProductTile(ProductDto product, AppLanguage language)
    {
        this.product = product;
        this.language = language;
    }

    public ProductDto Product
    {
        get => product;
        set
        {
            if (SetProperty(ref product, value))
            {
                RaiseAll();
            }
        }
    }

    public int InCart
    {
        get => inCart;
        set
        {
            if (SetProperty(ref inCart, value))
            {
                RaiseAll();
            }
        }
    }

    public AppLanguage Language
    {
        get => language;
        set
        {
            if (SetProperty(ref language, value))
            {
                RaiseAll();
            }
        }
    }

    public int Id => Product.Id;

    public string Name => Receipts.ReceiptLayout.ProductName(Product.NameEn, Product.NameAr, Language);

    /// <summary>US dollars.</summary>
    public decimal Price => Product.Price;

    /// <summary>Stock left once the cart is sold.</summary>
    public int Left => Math.Max(0, Product.Stock - InCart);

    public StockState State => Left <= 0 ? StockState.Out : Left < Product.MinStock ? StockState.Low : StockState.Ok;

    public bool CanAdd => Product.IsActive && Left > 0;

    public bool IsInCart => InCart > 0;

    public string InCartText => "×" + NumberText.Format(InCart);

    public string StockText => State switch
    {
        StockState.Out => CafeteriaText.OutOfStock.For(Language),
        StockState.Low => CafeteriaText.LowStockLeft.Format(Language, NumberText.Format(Left)),
        _ => CafeteriaText.StockLeft.Format(Language, NumberText.Format(Left)),
    };

    public Geometry Icon => CafeteriaIcons.ForCategory(Product.CategoryNameEn);

    /// <summary>Screen reader text: "Add Water 500 ml, $0.25".</summary>
    public string AccessibleName => CafeteriaText.AddItem.Format(Language, Name, Money.Format(Price, Language));

    private void RaiseAll() => OnPropertyChanged(string.Empty);
}

/// <summary>A category filter on the cashier screen ("All" has no id).</summary>
public sealed class CategoryTab : ObservableObject
{
    private bool isSelected;
    private string name = "";
    private int count;

    public CategoryTab(int? id) => Id = id;

    public int? Id { get; }

    public string Name
    {
        get => name;
        set => SetProperty(ref name, value);
    }

    public int Count
    {
        get => count;
        set => SetProperty(ref count, value);
    }

    public bool IsSelected
    {
        get => isSelected;
        set => SetProperty(ref isSelected, value);
    }
}

/// <summary>An item the server could not sell: how many are in the cart and how many are in stock.</summary>
public sealed record Shortage(int ProductId, string NameEn, string NameAr, int InCart, int Available);

/// <summary>The result of Charge: the receipt, or the short items, or another error.</summary>
public sealed record ChargeOutcome(ReceiptDto? Receipt, IReadOnlyList<Shortage> Shortages, Exception? Error)
{
    public bool IsPaid => Receipt is not null;
}

/// <summary>What a barcode scan did.</summary>
public sealed record ScanOutcome(string Code, ProductDto? Product, AddResult? Result)
{
    public bool IsUnknown => Product is null;
}

/// <summary>
/// The cashier screen without WPF controls: products and categories from the API, the filter (category tabs and
/// search), the cart, barcode scans, Charge (one <c>POST /api/sales</c> with the station name) and the current
/// shift's totals. Live stock and product updates are applied with <see cref="ApplyStock"/> and
/// <see cref="ApplyProduct"/>. Unit tested on <see cref="Gym.Client.Sample.SampleGymApi"/>.
/// </summary>
public sealed class CashierViewModel : ObservableObject
{
    private readonly IGymApi api;
    private readonly Func<string> station;
    private readonly Dictionary<int, ProductTile> tiles = [];
    private List<ProductDto> products = [];
    private List<CategoryDto> categories = [];
    private int? categoryId;
    private string search = "";
    private AppLanguage language;
    private ReceiptDto? lastReceipt;
    private ShiftSummaryDto? shift;
    private Currency payCurrency = Currency.Usd;

    /// <param name="rates">The exchange rate to use (default: the station's <see cref="ExchangeRates.Current"/>).</param>
    public CashierViewModel(IGymApi api, Func<string> station, AppLanguage language, ExchangeRates? rates = null)
    {
        ArgumentNullException.ThrowIfNull(api);
        ArgumentNullException.ThrowIfNull(station);
        this.api = api;
        this.station = station;
        this.language = language;
        Rates = rates ?? ExchangeRates.Current;
        Cart.Language = language;
        Cart.Lines.CollectionChanged += (_, _) => SyncTiles();
        Cart.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Cart.ItemCount))
            {
                SyncTiles();
            }
        };
    }

    public Cart Cart { get; } = new();

    /// <summary>The exchange rate used for the amount in pounds.</summary>
    public ExchangeRates Rates { get; }

    /// <summary>The currency the customer pays in (pounds only while a rate exists; otherwise dollars are used).</summary>
    public Currency PayCurrency
    {
        get => payCurrency;
        set => SetProperty(ref payCurrency, value);
    }

    /// <summary>The currency really used: pounds need a rate.</summary>
    public Currency EffectiveCurrency => PayCurrency == Currency.Syp && Rates.HasRate ? Currency.Syp : Currency.Usd;

    /// <summary>What the customer pays for the cart: the dollars, or the pounds at the rate (rounded with the admin's step).</summary>
    public PaymentAmount AmountDue => Rates.Due(Cart.Total, EffectiveCurrency);

    /// <summary>The cart total in pounds at the rate (to show next to the dollars), or null without a rate.</summary>
    public decimal? TotalInSyp => Rates.ToSyp(Cart.Total);

    public ObservableCollection<CategoryTab> Categories { get; } = [];

    /// <summary>The tiles that match the category and the search, in the API's order.</summary>
    public ObservableCollection<ProductTile> Tiles { get; } = [];

    public IReadOnlyList<ProductDto> Products => products;

    /// <summary>Null = all categories.</summary>
    public int? CategoryId
    {
        get => categoryId;
        set
        {
            if (SetProperty(ref categoryId, value))
            {
                foreach (var tab in Categories)
                {
                    tab.IsSelected = tab.Id == value;
                }

                Refilter();
            }
        }
    }

    public string Search
    {
        get => search;
        set
        {
            if (SetProperty(ref search, value ?? ""))
            {
                Refilter();
            }
        }
    }

    public AppLanguage Language
    {
        get => language;
        set
        {
            if (SetProperty(ref language, value))
            {
                foreach (var tile in tiles.Values)
                {
                    tile.Language = value;
                }

                Cart.Language = value;
                UpdateTabNames();
                OnPropertyChanged(nameof(LowStock));
            }
        }
    }

    /// <summary>The last sale of this screen (for Reprint).</summary>
    public ReceiptDto? LastReceipt
    {
        get => lastReceipt;
        private set => SetProperty(ref lastReceipt, value);
    }

    /// <summary>The logged-in cashier's open shift with its totals, or null when no shift is open.</summary>
    public ShiftSummaryDto? Shift
    {
        get => shift;
        private set => SetProperty(ref shift, value);
    }

    /// <summary>Active items below their minimum stock (the low-stock banner), lowest first.</summary>
    public IReadOnlyList<ProductDto> LowStock =>
        products.Where(p => p.IsActive && p.IsLowStock).OrderBy(p => p.Stock).ThenBy(p => p.NameEn, StringComparer.OrdinalIgnoreCase).ToList();

    public async Task LoadAsync(CancellationToken ct = default)
    {
        var loadedCategories = await api.GetCategoriesAsync(ct: ct).ConfigureAwait(true);
        var loadedProducts = await api.GetProductsAsync(ct: ct).ConfigureAwait(true);
        categories = loadedCategories.ToList();
        products = loadedProducts.Where(p => p.IsActive).ToList();
        foreach (var product in products)
        {
            Cart.UpdateProduct(product);
        }

        BuildTabs();
        Refilter();
        OnPropertyChanged(nameof(LowStock));
        await RefreshShiftAsync(ct).ConfigureAwait(true);
    }

    public async Task RefreshShiftAsync(CancellationToken ct = default)
    {
        var current = await api.GetCurrentShiftAsync(ct).ConfigureAwait(true);
        Shift = current is null ? null : await api.GetShiftSummaryAsync(current.Id, ct).ConfigureAwait(true);
    }

    public AddResult Add(ProductDto product)
    {
        ArgumentNullException.ThrowIfNull(product);
        var known = products.FirstOrDefault(p => p.Id == product.Id) ?? product;
        return Cart.Add(known);
    }

    /// <summary>A scanned (or typed) barcode: the product is looked up here first, then on the server.</summary>
    public async Task<ScanOutcome> ScanAsync(string code, CancellationToken ct = default)
    {
        var trimmed = code?.Trim() ?? "";
        if (trimmed.Length == 0)
        {
            return new ScanOutcome(trimmed, null, null);
        }

        var product = products.FirstOrDefault(p => string.Equals(p.Barcode, trimmed, StringComparison.Ordinal))
                      ?? await api.FindProductByBarcodeAsync(trimmed, ct).ConfigureAwait(true);
        if (product is null)
        {
            return new ScanOutcome(trimmed, null, null);
        }

        return new ScanOutcome(trimmed, product, Add(product));
    }

    /// <summary>
    /// Sells the cart (cash, in <see cref="PayCurrency"/>; <paramref name="cashReceived"/> is in that currency). On success the cart is emptied, the stock shown goes down and the receipt is
    /// <see cref="LastReceipt"/>. When the server refuses with <c>insufficient_stock</c>, the stock is reloaded, the
    /// short lines are marked in the cart and listed in the outcome. Other errors come back in the outcome.
    /// </summary>
    public async Task<ChargeOutcome> ChargeAsync(decimal? cashReceived, CancellationToken ct = default)
    {
        if (Cart.IsEmpty)
        {
            return new ChargeOutcome(null, [], new GymApiException(HttpStatusCode.BadRequest, "empty_sale", "The sale has no items."));
        }

        var sold = Cart.Lines.Select(l => (l.ProductId, l.Quantity)).ToList();
        ReceiptDto receipt;
        try
        {
            var payment = Rates.Payment(EffectiveCurrency);
            receipt = await api.CreateSaleAsync(new CreateSaleRequest(station(), Cart.ToSaleLines(), cashReceived, payment), ct).ConfigureAwait(true);
        }
        catch (GymApiException ex) when (ex.Code == "insufficient_stock")
        {
            var available = ReadShortages(ex.Body);
            await ReloadProductsAsync(ct).ConfigureAwait(true);
            if (available.Count == 0)
            {
                // No details in the answer: compare the cart with the stock just loaded.
                available = Cart.Lines
                    .Select(l => (l.ProductId, Stock: products.FirstOrDefault(p => p.Id == l.ProductId)?.Stock ?? 0, l.Quantity))
                    .Where(x => x.Quantity > x.Stock)
                    .ToDictionary(x => x.ProductId, x => Math.Max(0, x.Stock));
            }

            Cart.MarkShortages(available);
            var shortages = Cart.Lines
                .Where(l => available.ContainsKey(l.ProductId) && l.Quantity > available[l.ProductId])
                .Select(l => new Shortage(l.ProductId, l.Product.NameEn, l.Product.NameAr, l.Quantity, available[l.ProductId]))
                .ToList();
            return new ChargeOutcome(null, shortages, ex);
        }
        catch (Exception ex) when (ex is GymApiException or HttpRequestException or TaskCanceledException)
        {
            return new ChargeOutcome(null, [], ex);
        }

        // The sale is saved. Show the stock as the server has it now (a live update may come before or after).
        LastReceipt = receipt;
        Cart.Clear();
        try
        {
            await ReloadProductsAsync(ct).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is GymApiException or HttpRequestException or TaskCanceledException)
        {
            // No answer now: take the sold pieces off locally; the next live update or visit corrects it.
            foreach (var (productId, quantity) in sold)
            {
                UpdateStock(productId, stock => stock - quantity);
            }

            Refilter();
            OnPropertyChanged(nameof(LowStock));
        }

        try
        {
            await RefreshShiftAsync(ct).ConfigureAwait(true);
        }
        catch (Exception ex) when (ex is GymApiException or HttpRequestException or TaskCanceledException)
        {
            // The shift totals refresh with the next sale or visit.
        }

        return new ChargeOutcome(receipt, [], null);
    }

    /// <summary>Live update (another till sold, stock-in, adjustment): new stock levels.</summary>
    public void ApplyStock(IEnumerable<StockLevelDto> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        foreach (var level in levels)
        {
            UpdateStock(level.ProductId, _ => level.Stock, level.MinStock);
        }

        OnPropertyChanged(nameof(LowStock));
    }

    /// <summary>Live update: a product was created or edited (price, name, barcode, active).</summary>
    public void ApplyProduct(ProductDto product)
    {
        ArgumentNullException.ThrowIfNull(product);
        var index = products.FindIndex(p => p.Id == product.Id);
        if (product.IsActive)
        {
            if (index >= 0)
            {
                products[index] = product;
            }
            else
            {
                products.Add(product);
            }

            if (tiles.TryGetValue(product.Id, out var tile))
            {
                tile.Product = product;
            }

            Cart.UpdateProduct(product);
        }
        else if (index >= 0)
        {
            products.RemoveAt(index);
            tiles.Remove(product.Id);
        }

        BuildTabs();
        Refilter();
        OnPropertyChanged(nameof(LowStock));
    }

    /// <summary>Reads <c>shortages: [{ productId, available }]</c> from an <c>insufficient_stock</c> answer.</summary>
    public static Dictionary<int, int> ReadShortages(string? problemJson)
    {
        var result = new Dictionary<int, int>();
        if (string.IsNullOrWhiteSpace(problemJson))
        {
            return result;
        }

        try
        {
            using var json = JsonDocument.Parse(problemJson);
            if (json.RootElement.ValueKind != JsonValueKind.Object || !TryGet(json.RootElement, "shortages", out var list) || list.ValueKind != JsonValueKind.Array)
            {
                return result;
            }

            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object
                    && TryGet(item, "productId", out var id) && id.TryGetInt32(out var productId)
                    && TryGet(item, "available", out var stock) && stock.TryGetInt32(out var available))
                {
                    result[productId] = Math.Max(0, available);
                }
            }
        }
        catch (JsonException)
        {
            // Not problem details: the caller compares with fresh stock instead.
        }

        return result;
    }

    private static bool TryGet(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private async Task ReloadProductsAsync(CancellationToken ct)
    {
        var loaded = await api.GetProductsAsync(ct: ct).ConfigureAwait(true);
        products = loaded.Where(p => p.IsActive).ToList();
        foreach (var product in products)
        {
            Cart.UpdateProduct(product);
            if (tiles.TryGetValue(product.Id, out var tile))
            {
                tile.Product = product;
            }
        }

        Refilter();
        OnPropertyChanged(nameof(LowStock));
    }

    private void UpdateStock(int productId, Func<int, int> stock, int? minStock = null)
    {
        var index = products.FindIndex(p => p.Id == productId);
        if (index < 0)
        {
            return;
        }

        var old = products[index];
        var newStock = stock(old.Stock);
        var min = minStock ?? old.MinStock;
        var updated = old with { Stock = newStock, MinStock = min, IsLowStock = StockRules.IsLow(newStock, min) };
        products[index] = updated;
        if (tiles.TryGetValue(productId, out var tile))
        {
            tile.Product = updated;
        }

        Cart.UpdateProduct(updated);
    }

    private void BuildTabs()
    {
        var selected = categoryId;
        Categories.Clear();
        Categories.Add(new CategoryTab(null));
        foreach (var category in categories.Where(c => c.IsActive))
        {
            Categories.Add(new CategoryTab(category.Id));
        }

        if (selected is not null && Categories.All(t => t.Id != selected))
        {
            categoryId = null;
            OnPropertyChanged(nameof(CategoryId));
        }

        foreach (var tab in Categories)
        {
            tab.IsSelected = tab.Id == categoryId;
            tab.Count = tab.Id is null ? products.Count : products.Count(p => p.CategoryId == tab.Id);
        }

        UpdateTabNames();
    }

    private void UpdateTabNames()
    {
        foreach (var tab in Categories)
        {
            var category = categories.FirstOrDefault(c => c.Id == tab.Id);
            tab.Name = category is null
                ? CafeteriaText.AllCategories.For(language)
                : Receipts.ReceiptLayout.ProductName(category.NameEn, category.NameAr, language);
        }
    }

    private void Refilter()
    {
        var query = SearchText.Normalize(search);
        var visible = products
            .Where(p => categoryId is null || p.CategoryId == categoryId)
            .Where(p => query.Length == 0
                        || SearchText.Normalize(p.NameEn).Contains(query, StringComparison.Ordinal)
                        || SearchText.Normalize(p.NameAr).Contains(query, StringComparison.Ordinal)
                        || (p.Barcode is { } barcode && barcode.Contains(search.Trim(), StringComparison.Ordinal)))
            .ToList();

        Tiles.Clear();
        foreach (var product in visible)
        {
            if (!tiles.TryGetValue(product.Id, out var tile))
            {
                tile = new ProductTile(product, language);
                tiles[product.Id] = tile;
            }

            tile.Product = product;
            tile.InCart = Cart.QuantityOf(product.Id);
            Tiles.Add(tile);
        }
    }

    private void SyncTiles()
    {
        foreach (var tile in tiles.Values)
        {
            tile.InCart = Cart.QuantityOf(tile.Id);
        }
    }
}
