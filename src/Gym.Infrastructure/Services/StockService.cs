namespace Gym.Infrastructure.Services;

/// <summary>Stock-in, adjustments and stock levels. Current stock = sum of the stock movements.</summary>
public sealed class StockService(GymDbContext db, GymClock clock)
{
    public const int MaxStockIn = 1_000_000;
    public const int MaxListSize = 1000;

    public async Task<StockLevelDto> StockInAsync(StockInRequest request, int staffId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Guard.Range(request.Quantity, "Quantity", 1, MaxStockIn);
        var note = Guard.Optional(request.Note, "Note", StockMovement.NoteMaxLength);
        if (!await db.Products.AnyAsync(p => p.Id == request.ProductId, ct))
        {
            throw ProductService.ProductNotFound(request.ProductId);
        }

        db.StockMovements.Add(new StockMovement
        {
            ProductId = request.ProductId,
            Quantity = request.Quantity,
            Type = StockMovementType.StockIn,
            Note = note,
            StaffId = staffId,
            At = clock.UtcNow,
        });
        await db.SaveChangesAsync(ct);
        return (await GetLevelsAsync([request.ProductId], lowOnly: false, ct)).Single();
    }

    /// <summary>Corrects the stock by a change or to a counted quantity. The stock cannot go below zero.</summary>
    public async Task<StockLevelDto> AdjustAsync(StockAdjustmentRequest request, int staffId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var note = Guard.Optional(request.Note, "Note", StockMovement.NoteMaxLength);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var products = await LockProductsAsync([request.ProductId], ct);
        if (products.Count == 0)
        {
            throw ProductService.ProductNotFound(request.ProductId);
        }

        var current = (await CurrentStockAsync([request.ProductId], ct)).GetValueOrDefault(request.ProductId);
        var delta = StockRules.AdjustmentDelta(current, request.Delta, request.CountedQuantity);
        if (delta != 0)
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = request.ProductId,
                Quantity = delta,
                Type = StockMovementType.Adjustment,
                Note = note,
                StaffId = staffId,
                At = clock.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return (await GetLevelsAsync([request.ProductId], lowOnly: false, ct)).Single();
    }

    /// <summary>Stock of the given products (any state), or of all active products when <paramref name="productIds"/> is null.</summary>
    public async Task<IReadOnlyList<StockLevelDto>> GetLevelsAsync(IReadOnlyCollection<int>? productIds, bool lowOnly, CancellationToken ct = default)
    {
        var query = db.Products.AsNoTracking();
        if (productIds is null)
        {
            query = query.Where(p => p.IsActive);
        }
        else
        {
            var ids = productIds.ToList();
            query = query.Where(p => ids.Contains(p.Id));
        }

        var rows = await query
            .OrderBy(p => p.NameEn).ThenBy(p => p.Id)
            .Select(p => new { p.Id, p.NameEn, p.NameAr, p.MinStock, Stock = p.Movements.Sum(m => m.Quantity) })
            .ToListAsync(ct);

        return rows
            .Select(r => new StockLevelDto(r.Id, r.NameEn, r.NameAr, r.Stock, r.MinStock, StockRules.IsLow(r.Stock, r.MinStock)))
            .Where(l => !lowOnly || l.IsLow)
            .ToList();
    }

    public async Task<IReadOnlyList<StockMovementDto>> ListMovementsAsync(int? productId, DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var (start, end) = clock.UtcRange(from, to);
        var query = db.StockMovements.AsNoTracking().Where(m => m.At >= start && m.At < end);
        if (productId is { } id)
        {
            query = query.Where(m => m.ProductId == id);
        }

        return await query
            .OrderByDescending(m => m.At).ThenByDescending(m => m.Id)
            .Take(MaxListSize)
            .Select(m => new StockMovementDto(
                m.Id, m.ProductId, m.Product!.NameEn, m.Product.NameAr, m.Quantity, m.Type, m.SaleId, m.Note, m.Staff!.FullName, m.At))
            .ToListAsync(ct);
    }

    /// <summary>
    /// Locks the product rows (in id order, so two tills never deadlock) until the transaction ends.
    /// Stock read after this sees every sale committed before.
    /// </summary>
    internal Task<List<Product>> LockProductsAsync(int[] productIds, CancellationToken ct) =>
        db.Products.FromSql($"SELECT * FROM products WHERE id = ANY({productIds}) ORDER BY id FOR UPDATE").ToListAsync(ct);

    internal Task<Dictionary<int, int>> CurrentStockAsync(IReadOnlyCollection<int> productIds, CancellationToken ct)
    {
        var ids = productIds.ToList();
        return db.StockMovements
            .Where(m => ids.Contains(m.ProductId))
            .GroupBy(m => m.ProductId)
            .Select(g => new { ProductId = g.Key, Quantity = g.Sum(m => m.Quantity) })
            .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, ct);
    }
}
