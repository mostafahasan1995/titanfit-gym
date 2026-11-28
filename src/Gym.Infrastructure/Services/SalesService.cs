namespace Gym.Infrastructure.Services;

/// <summary>
/// Cafeteria sales. A sale saves its lines and stock movements in one transaction; the product rows are
/// locked first, so two tills selling the last items at the same moment can never oversell.
/// </summary>
public sealed class SalesService(GymDbContext db, GymClock clock, StockService stock, ExchangeRateService rates)
{
    public const int MaxListSize = 1000;

    public async Task<ReceiptDto> CreateAsync(CreateSaleRequest request, int cashierId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var station = Guard.Required(request.Station, "Station", CheckIn.StationMaxLength);
        if (request.Lines is null || request.Lines.Count == 0)
        {
            throw new DomainException("empty_sale", "Add at least one item.");
        }

        if (request.Lines.Any(l => l is null || l.Quantity < 1 || l.Quantity > Sale.MaxLineQuantity))
        {
            throw new DomainException("invalid_quantity", $"Each quantity must be 1 to {Sale.MaxLineQuantity}.");
        }

        var wanted = request.Lines
            .GroupBy(l => l.ProductId)
            .Select(g => (ProductId: g.Key, Quantity: g.Sum(l => l.Quantity)))
            .OrderBy(w => w.ProductId)
            .ToList();
        var ids = wanted.Select(w => w.ProductId).ToArray();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var products = (await stock.LockProductsAsync(ids, ct)).ToDictionary(p => p.Id);
        var missing = ids.FirstOrDefault(id => !products.ContainsKey(id));
        if (missing != 0 || products.Count != ids.Length)
        {
            throw ProductService.ProductNotFound(missing);
        }

        var inactive = products.Values.FirstOrDefault(p => !p.IsActive);
        if (inactive is not null)
        {
            throw new DomainException("product_inactive", $"{inactive.NameEn} is not for sale.");
        }

        var available = await stock.CurrentStockAsync(ids, ct);
        StockRules.EnsureAvailable(wanted.Select(w => (products[w.ProductId], available.GetValueOrDefault(w.ProductId), w.Quantity)));

        // One cashier's sales run one after the other, so a first sale on two tills opens only one shift.
        await ShiftService.LockCashierAsync(db, cashierId, ct);
        var now = clock.UtcNow;
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.CashierId == cashierId && s.ClosedAt == null, ct);
        if (shift is null)
        {
            // Selling without an open shift opens one, so every sale belongs to a shift.
            shift = new Shift { CashierId = cashierId, Station = station, OpenedAt = now };
            db.Shifts.Add(shift);
        }

        var sale = new Sale { Station = station, CashierId = cashierId, Shift = shift, At = now };
        foreach (var (productId, quantity) in wanted)
        {
            sale.AddLine(products[productId], quantity);
            db.StockMovements.Add(new StockMovement
            {
                ProductId = productId,
                Quantity = -quantity,
                Type = StockMovementType.Sale,
                Sale = sale,
                StaffId = cashierId,
                At = now,
            });
        }

        var payment = await rates.GetDueAsync(sale.Total, request.Payment, ct);
        sale.SetPayment(payment, request.CashReceived);
        db.Sales.Add(sale);
        await db.SaveChangesOrConflictAsync(ct);
        await transaction.CommitAsync(ct);
        return await GetReceiptAsync(sale.Id, ct);
    }

    public async Task<ReceiptDto> GetReceiptAsync(long id, CancellationToken ct = default)
    {
        var sale = await db.Sales.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new
            {
                s.Id,
                s.Number,
                s.At,
                s.Station,
                s.CashierId,
                CashierName = s.Cashier!.FullName,
                s.ShiftId,
                s.Total,
                s.CashReceived,
                s.Currency,
                s.PaidAmount,
                s.ExchangeRate,
            })
            .SingleOrDefaultAsync(ct) ?? throw new NotFoundException("sale_not_found", $"Sale {id} does not exist.");

        var lines = await db.SaleLines.AsNoTracking()
            .Where(l => l.SaleId == id)
            .OrderBy(l => l.Id)
            .Select(l => new ReceiptLineDto(l.ProductId, l.Product!.NameEn, l.Product.NameAr, l.Quantity, l.UnitPrice, l.LineTotal))
            .ToListAsync(ct);

        return new ReceiptDto(
            sale.Id, sale.Number, sale.At, sale.Station, sale.CashierId, sale.CashierName, sale.ShiftId,
            lines, sale.Total, sale.CashReceived, sale.CashReceived - sale.PaidAmount, sale.Currency, sale.PaidAmount, sale.ExchangeRate);
    }

    public async Task<IReadOnlyList<SaleSummaryDto>> ListAsync(DateOnly from, DateOnly to, int? cashierId, CancellationToken ct = default)
    {
        var (start, end) = clock.UtcRange(from, to);
        var query = db.Sales.AsNoTracking().Where(s => s.At >= start && s.At < end);
        if (cashierId is { } id)
        {
            query = query.Where(s => s.CashierId == id);
        }

        return await query
            .OrderByDescending(s => s.At).ThenByDescending(s => s.Id)
            .Take(MaxListSize)
            .Select(s => new SaleSummaryDto(s.Id, s.Number, s.At, s.Station, s.CashierId, s.Cashier!.FullName, s.Total, s.Lines.Sum(l => l.Quantity), s.Currency, s.PaidAmount))
            .ToListAsync(ct);
    }
}
