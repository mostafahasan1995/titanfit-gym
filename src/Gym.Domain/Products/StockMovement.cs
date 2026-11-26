namespace Gym.Domain;

public enum StockMovementType
{
    StockIn = 0,
    Sale = 1,
    Adjustment = 2,
}

/// <summary>Every stock change is one row. Current stock = sum of <see cref="Quantity"/>.</summary>
public class StockMovement
{
    public const int NoteMaxLength = 500;

    public long Id { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    /// <summary>Positive for stock-in, negative for a sale, either sign for an adjustment.</summary>
    public int Quantity { get; set; }

    public StockMovementType Type { get; set; }

    public long? SaleId { get; set; }

    public Sale? Sale { get; set; }

    public string? Note { get; set; }

    public int StaffId { get; set; }

    public StaffMember? Staff { get; set; }

    public DateTime At { get; set; }
}

/// <summary>A sale line asks for more than the current stock.</summary>
public sealed class InsufficientStockException(IReadOnlyList<StockShortage> shortages)
    : DomainException(
        "insufficient_stock",
        "Not enough stock: " + string.Join(", ", shortages.Select(s => $"{s.ProductName} (in stock {s.Available}, requested {s.Requested})")) + ".",
        new Dictionary<string, object?> { ["shortages"] = shortages })
{
    public IReadOnlyList<StockShortage> Shortages { get; } = shortages;
}

public sealed record StockShortage(int ProductId, string ProductName, int Available, int Requested);

/// <summary>
/// Stock rules. Decision (step 2.2, the owner can change it): stock never goes below zero. A sale line that
/// asks for more than the current stock is refused with a clear error, and so is an adjustment that would
/// leave a negative stock.
/// </summary>
public static class StockRules
{
    public static bool IsLow(int currentStock, int minStock) => currentStock < minStock;

    /// <summary>Throws <see cref="InsufficientStockException"/> listing every line that asks for more than is in stock.</summary>
    public static void EnsureAvailable(IEnumerable<(Product Product, int Available, int Requested)> lines)
    {
        var shortages = lines
            .Where(l => l.Requested > l.Available)
            .Select(l => new StockShortage(l.Product.Id, l.Product.NameEn, Math.Max(l.Available, 0), l.Requested))
            .ToList();
        if (shortages.Count > 0)
        {
            throw new InsufficientStockException(shortages);
        }
    }

    /// <summary>Validates an adjustment and returns the quantity delta to store.</summary>
    public static int AdjustmentDelta(int currentStock, int? delta, int? countedQuantity)
    {
        if (delta.HasValue == countedQuantity.HasValue)
        {
            throw new DomainException("invalid_adjustment", "Give either the change (delta) or the counted quantity.");
        }

        var change = countedQuantity.HasValue ? countedQuantity.Value - currentStock : delta.GetValueOrDefault();
        if ((long)currentStock + change < 0)
        {
            throw new DomainException(
                "negative_stock",
                $"The stock cannot go below zero (in stock {currentStock}, change {change}).");
        }

        return change;
    }
}
