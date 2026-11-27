using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using Gym.Cafeteria.Receipts;
using Gym.Client;
using Gym.Contracts;
using Gym.Domain;
using Gym.UI;
using Gym.UI.Controls;

namespace Gym.Cafeteria.Inventory;

/// <summary>One item in the stock list.</summary>
public sealed class StockRow : ObservableObject
{
    private ProductDto product;
    private AppLanguage language;

    public StockRow(ProductDto product, AppLanguage language)
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
                OnPropertyChanged(string.Empty);
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
                OnPropertyChanged(string.Empty);
            }
        }
    }

    public int Id => Product.Id;

    public string Name => ReceiptLayout.ProductName(Product.NameEn, Product.NameAr, Language);

    public string CategoryName => ReceiptLayout.ProductName(Product.CategoryNameEn, Product.CategoryNameAr, Language);

    public string Barcode => Product.Barcode ?? "—";

    public long Price => Product.Price;

    public int Stock => Product.Stock;

    public string StockText => NumberText.Format(Product.Stock);

    public string MinStockText => NumberText.Format(Product.MinStock);

    /// <summary>"min 10" under the stock number.</summary>
    public string MinText => BidiText.FixNumbers(CafeteriaText.MinShort.Format(Language, NumberText.Format(Product.MinStock)), Language);

    public Geometry Icon => CafeteriaIcons.ForCategory(Product.CategoryNameEn);

    public bool IsLow => Product.IsActive && Product.IsLowStock;

    public bool IsOut => Product.IsActive && Product.Stock <= 0;

    public bool IsInactive => !Product.IsActive;

    public bool HasStatus => IsLow || IsOut || IsInactive;

    public PillTone StatusTone => IsInactive ? PillTone.Neutral : IsOut ? PillTone.Bad : IsLow ? PillTone.Warning : PillTone.Good;

    public string StatusText => (IsInactive ? CafeteriaText.Inactive : IsOut ? CafeteriaText.OutShort : IsLow ? CafeteriaText.LowStock : CafeteriaText.InStock).For(Language);
}

/// <summary>One stock movement in the history of an item.</summary>
public sealed record MovementRow(StockMovementDto Movement, AppLanguage Language)
{
    public string When => DateText.DateAndTime(Movement.At, Language);

    public string TypeText => (Movement.Type switch
    {
        StockMovementType.StockIn => CafeteriaText.MovementStockIn,
        StockMovementType.Sale => CafeteriaText.MovementSale,
        _ => CafeteriaText.MovementAdjustment,
    }).For(Language);

    public PillTone Tone => Movement.Type switch
    {
        StockMovementType.StockIn => PillTone.Good,
        StockMovementType.Sale => PillTone.Info,
        _ => PillTone.Warning,
    };

    /// <summary>"+24" or "−2".</summary>
    public string QuantityText => StockForms.Signed(Movement.Quantity);

    public bool IsIncrease => Movement.Quantity > 0;

    /// <summary>The note (if any) and the staff member.</summary>
    public string Detail
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(Movement.Note))
            {
                parts.Add(Movement.Note.Trim());
            }

            parts.Add(Movement.StaffName);
            return string.Join(" · ", parts);
        }
    }
}

/// <summary>
/// The stock screen without WPF controls: items with their stock (also inactive ones on request), the filters
/// (category, low only, inactive, search), the selected item's movements, stock-in, adjustments and product
/// editing. Unit tested on the sample API.
/// </summary>
public sealed class StockViewModel : ObservableObject
{
    public const int HistoryDays = 90;

    private readonly IGymApi api;
    private readonly Dictionary<int, StockRow> rows = [];
    private List<ProductDto> products = [];
    private List<CategoryDto> categories = [];
    private int? categoryId;
    private bool lowOnly;
    private bool includeInactive;
    private string search = "";
    private AppLanguage language;
    private StockRow? selected;

    public StockViewModel(IGymApi api, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(api);
        this.api = api;
        this.language = language;
    }

    /// <summary>The items that pass the filters.</summary>
    public ObservableCollection<StockRow> Rows { get; } = [];

    /// <summary>The selected item's last stock movements, newest first.</summary>
    public ObservableCollection<MovementRow> Movements { get; } = [];

    /// <summary>Active categories (for the filter and the product form).</summary>
    public IReadOnlyList<CategoryDto> Categories => categories;

    public IReadOnlyList<ProductDto> Products => products;

    /// <summary>Active items below their minimum stock.</summary>
    public int LowCount => products.Count(p => p.IsActive && p.IsLowStock);

    public int? CategoryId
    {
        get => categoryId;
        set
        {
            if (SetProperty(ref categoryId, value))
            {
                Refilter();
            }
        }
    }

    public bool LowOnly
    {
        get => lowOnly;
        set
        {
            if (SetProperty(ref lowOnly, value))
            {
                Refilter();
            }
        }
    }

    public bool IncludeInactive
    {
        get => includeInactive;
        set
        {
            if (SetProperty(ref includeInactive, value))
            {
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
                foreach (var row in rows.Values)
                {
                    row.Language = value;
                }

                var movements = Movements.Select(m => m with { Language = value }).ToList();
                Movements.Clear();
                movements.ForEach(Movements.Add);
            }
        }
    }

    public StockRow? Selected
    {
        get => selected;
        private set => SetProperty(ref selected, value);
    }

    public async Task LoadAsync(CancellationToken ct = default)
    {
        categories = (await api.GetCategoriesAsync(ct: ct).ConfigureAwait(true)).ToList();
        products = (await api.GetProductsAsync(includeInactive: true, ct: ct).ConfigureAwait(true)).ToList();
        foreach (var product in products)
        {
            if (rows.TryGetValue(product.Id, out var row))
            {
                row.Product = product;
            }
        }

        Refilter();
        OnPropertyChanged(nameof(LowCount));
        OnPropertyChanged(nameof(Categories));
        var keep = Selected is { } current ? Rows.FirstOrDefault(r => r.Id == current.Id) : null;
        await SelectAsync(keep ?? Rows.FirstOrDefault(), ct).ConfigureAwait(true);
    }

    /// <summary>Shows an item and loads its movement history (the last <see cref="HistoryDays"/> days).</summary>
    public async Task SelectAsync(StockRow? row, CancellationToken ct = default)
    {
        Selected = row;
        Movements.Clear();
        if (row is null)
        {
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var history = await api.GetStockMovementsAsync(row.Id, today.AddDays(-HistoryDays), today, ct).ConfigureAwait(true);
        if (!ReferenceEquals(Selected, row))
        {
            return;
        }

        foreach (var movement in history.Take(200))
        {
            Movements.Add(new MovementRow(movement, language));
        }
    }

    public async Task<StockLevelDto> StockInAsync(int productId, int quantity, string? note, CancellationToken ct = default)
    {
        var level = await api.StockInAsync(new StockInRequest(productId, quantity, Clean(note)), ct).ConfigureAwait(true);
        await AfterStockChangeAsync(level, ct).ConfigureAwait(true);
        return level;
    }

    /// <summary>A correction by <paramref name="delta"/> or to a <paramref name="counted"/> quantity (give one).</summary>
    public async Task<StockLevelDto> AdjustAsync(int productId, int? delta, int? counted, string? reason, CancellationToken ct = default)
    {
        var level = await api.AdjustStockAsync(new StockAdjustmentRequest(productId, delta, counted, Clean(reason)), ct).ConfigureAwait(true);
        await AfterStockChangeAsync(level, ct).ConfigureAwait(true);
        return level;
    }

    /// <summary>Creates (<paramref name="id"/> null) or updates a product, then shows it.</summary>
    public async Task<ProductDto> SaveProductAsync(int? id, SaveProductRequest request, CancellationToken ct = default)
    {
        var product = id is { } existing
            ? await api.UpdateProductAsync(existing, request, ct).ConfigureAwait(true)
            : await api.CreateProductAsync(request, ct).ConfigureAwait(true);
        ApplyProduct(product);
        if (Rows.FirstOrDefault(r => r.Id == product.Id) is { } row)
        {
            await SelectAsync(row, ct).ConfigureAwait(true);
        }

        return product;
    }

    /// <summary>Live update: new stock levels.</summary>
    public void ApplyStock(IEnumerable<StockLevelDto> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);
        foreach (var level in levels)
        {
            var index = products.FindIndex(p => p.Id == level.ProductId);
            if (index >= 0)
            {
                products[index] = products[index] with { Stock = level.Stock, MinStock = level.MinStock, IsLowStock = level.IsLow };
                if (rows.TryGetValue(level.ProductId, out var row))
                {
                    row.Product = products[index];
                }
            }
        }

        Refilter();
        OnPropertyChanged(nameof(LowCount));
    }

    /// <summary>Live update or after saving: a created or edited product.</summary>
    public void ApplyProduct(ProductDto product)
    {
        ArgumentNullException.ThrowIfNull(product);
        var index = products.FindIndex(p => p.Id == product.Id);
        if (index >= 0)
        {
            products[index] = product;
        }
        else
        {
            products.Add(product);
        }

        if (rows.TryGetValue(product.Id, out var row))
        {
            row.Product = product;
        }

        Refilter();
        OnPropertyChanged(nameof(LowCount));
    }

    /// <summary>True when the item passes the current filters.</summary>
    public bool Matches(ProductDto product)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (!includeInactive && !product.IsActive)
        {
            return false;
        }

        if (lowOnly && !(product.IsActive && product.IsLowStock))
        {
            return false;
        }

        if (categoryId is { } id && product.CategoryId != id)
        {
            return false;
        }

        var query = SearchText.Normalize(search);
        return query.Length == 0
               || SearchText.Normalize(product.NameEn).Contains(query, StringComparison.Ordinal)
               || SearchText.Normalize(product.NameAr).Contains(query, StringComparison.Ordinal)
               || (product.Barcode is { } barcode && barcode.Contains(search.Trim(), StringComparison.Ordinal));
    }

    private async Task AfterStockChangeAsync(StockLevelDto level, CancellationToken ct)
    {
        ApplyStock([level]);
        if (Selected is { } row && row.Id == level.ProductId)
        {
            await SelectAsync(row, ct).ConfigureAwait(true);
        }
    }

    private void Refilter()
    {
        var visible = products.Where(Matches).ToList();
        Rows.Clear();
        foreach (var product in visible)
        {
            if (!rows.TryGetValue(product.Id, out var row))
            {
                row = new StockRow(product, language);
                rows[product.Id] = row;
            }

            row.Product = product;
            Rows.Add(row);
        }
    }

    private static string? Clean(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}

/// <summary>Reading the numbers typed in the stock forms (Western or Arabic digits).</summary>
public static class StockForms
{
    public const int MaxQuantity = 1_000_000;

    /// <summary>A quantity of 1 or more (stock-in), or null.</summary>
    public static int? ParseQuantity(string? text) => ParseInt(text) is { } value and >= 1 and <= MaxQuantity ? value : null;

    /// <summary>A count of 0 or more (stocktake, minimum stock), or null.</summary>
    public static int? ParseCount(string? text) => ParseInt(text) is { } value and >= 0 and <= MaxQuantity ? value : null;

    /// <summary>A change that is not zero, with an optional sign ("-3", "+5", "−2"), or null.</summary>
    public static int? ParseChange(string? text) => ParseInt(text) is { } value and not 0 and >= -MaxQuantity and <= MaxQuantity ? value : null;

    /// <summary>"+24", "−2", "0" (a real minus sign; left-to-right marks keep the sign in front in Arabic).</summary>
    public static string Signed(int value) => value switch
    {
        > 0 => "‎+" + NumberText.Format(value),
        < 0 => "‎−" + NumberText.Format(-(long)value),
        _ => "0",
    };

    public static int? ParseInt(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var cleaned = SearchText.NormalizeDigits(text.Trim())
            .Replace("−", "-", StringComparison.Ordinal)
            .Replace(",", "", StringComparison.Ordinal)
            .Replace("٬", "", StringComparison.Ordinal)
            .Replace(" ", "", StringComparison.Ordinal);
        return int.TryParse(cleaned, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value) ? value : null;
    }
}
