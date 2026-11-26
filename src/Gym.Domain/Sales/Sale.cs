namespace Gym.Domain;

/// <summary>One cafeteria sale (a receipt). Paid in cash.</summary>
public class Sale
{
    public const int MaxLineQuantity = 10_000;

    public long Id { get; set; }

    /// <summary>Receipt number from a database sequence.</summary>
    public long Number { get; set; }

    public string Station { get; set; } = "";

    public int CashierId { get; set; }

    public StaffMember? Cashier { get; set; }

    public int? ShiftId { get; set; }

    public Shift? Shift { get; set; }

    public DateTime At { get; set; }

    /// <summary>Whole SYP: the sum of the line totals.</summary>
    public long Total { get; set; }

    /// <summary>Cash handed over by the customer, if the cashier typed it.</summary>
    public long? CashReceived { get; set; }

    public List<SaleLine> Lines { get; set; } = [];

    public long? Change => CashReceived - Total;

    /// <summary>Adds a line at the product's current price.</summary>
    public SaleLine AddLine(Product product, int quantity)
    {
        ArgumentNullException.ThrowIfNull(product);
        if (quantity is < 1 or > MaxLineQuantity)
        {
            throw new DomainException("invalid_quantity", $"The quantity must be 1 to {MaxLineQuantity}.");
        }

        var line = new SaleLine
        {
            ProductId = product.Id,
            Product = product,
            Quantity = quantity,
            UnitPrice = product.Price,
            LineTotal = checked(product.Price * quantity),
        };
        Lines.Add(line);
        Total = checked(Total + line.LineTotal);
        return line;
    }

    public void SetCashReceived(long? cashReceived)
    {
        if (cashReceived is { } cash && cash < Total)
        {
            throw new DomainException("cash_received_too_low", $"The cash received ({cash}) is less than the total ({Total}).");
        }

        CashReceived = cashReceived;
    }
}

public class SaleLine
{
    public long Id { get; set; }

    public long SaleId { get; set; }

    public Sale? Sale { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public int Quantity { get; set; }

    /// <summary>Price at the time of the sale.</summary>
    public long UnitPrice { get; set; }

    public long LineTotal { get; set; }
}
