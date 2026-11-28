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

    /// <summary>US dollars: the sum of the line totals. The sale's dollar value.</summary>
    public decimal Total { get; set; }

    /// <summary>The currency the customer paid in.</summary>
    public Currency Currency { get; set; } = Currency.Usd;

    /// <summary>The amount due in <see cref="Currency"/>: the total in dollars, or in pounds (rounded) at <see cref="ExchangeRate"/>.</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>The rate used (pounds for 1 dollar); null when no rate existed yet (a dollar payment).</summary>
    public decimal? ExchangeRate { get; set; }

    /// <summary>Cash handed over by the customer in <see cref="Currency"/>, if the cashier typed it.</summary>
    public decimal? CashReceived { get; set; }

    public List<SaleLine> Lines { get; set; } = [];

    /// <summary>The change in <see cref="Currency"/>.</summary>
    public decimal? Change => CashReceived - PaidAmount;

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

    /// <summary>
    /// Sets how the customer pays (after the lines are added): the currency, the amount due in it, the rate, and the
    /// cash handed over in that currency (optional; not less than the amount due).
    /// </summary>
    public void SetPayment(PaymentAmount payment, decimal? cashReceived)
    {
        ArgumentNullException.ThrowIfNull(payment);
        if (cashReceived is { } cash && cash < payment.Amount)
        {
            throw new DomainException("cash_received_too_low", $"The cash received ({cash}) is less than the amount due ({payment.Amount}).");
        }

        Currency = payment.Currency;
        PaidAmount = payment.Amount;
        ExchangeRate = payment.SypPerUsd;
        CashReceived = cashReceived;
    }

    /// <summary>Paid in dollars at the total (no rate).</summary>
    public void SetCashReceived(decimal? cashReceived) => SetPayment(new PaymentAmount(Currency.Usd, Total, null), cashReceived);
}

public class SaleLine
{
    public long Id { get; set; }

    public long SaleId { get; set; }

    public Sale? Sale { get; set; }

    public int ProductId { get; set; }

    public Product? Product { get; set; }

    public int Quantity { get; set; }

    /// <summary>Price at the time of the sale (US dollars).</summary>
    public decimal UnitPrice { get; set; }

    public decimal LineTotal { get; set; }
}
