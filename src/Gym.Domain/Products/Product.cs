namespace Gym.Domain;

/// <summary>A cafeteria category (Drinks, Shakes, Snacks, Supplements, ...).</summary>
public class ProductCategory
{
    public const int NameMaxLength = 100;

    public int Id { get; set; }

    public string NameEn { get; set; } = "";

    public string NameAr { get; set; } = "";

    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public static IReadOnlyList<ProductCategory> Defaults() =>
    [
        new() { NameEn = "Drinks", NameAr = "مشروبات", SortOrder = 1 },
        new() { NameEn = "Shakes", NameAr = "مخفوقات", SortOrder = 2 },
        new() { NameEn = "Snacks", NameAr = "وجبات خفيفة", SortOrder = 3 },
        new() { NameEn = "Supplements", NameAr = "مكملات", SortOrder = 4 },
    ];
}

/// <summary>A cafeteria item. Its stock is the sum of its stock movements.</summary>
public class Product
{
    public const int NameMaxLength = 200;
    public const int BarcodeMaxLength = 64;

    public int Id { get; set; }

    public string NameEn { get; set; } = "";

    public string NameAr { get; set; } = "";

    public int CategoryId { get; set; }

    public ProductCategory? Category { get; set; }

    /// <summary>Unique when present.</summary>
    public string? Barcode { get; set; }

    /// <summary>US dollars.</summary>
    public decimal Price { get; set; }

    /// <summary>Low stock = current stock below this number.</summary>
    public int MinStock { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public List<StockMovement> Movements { get; set; } = [];

    /// <summary>Trims; empty → null.</summary>
    public static string? NormalizeBarcode(string? barcode) =>
        string.IsNullOrWhiteSpace(barcode) ? null : barcode.Trim();
}
