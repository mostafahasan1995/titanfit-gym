namespace Gym.Infrastructure.Services;

/// <summary>Cashier shifts: open, close and the summary per cashier and shift.</summary>
public sealed class ShiftService(GymDbContext db, GymClock clock)
{
    /// <summary>Opens a shift, or returns the cashier's shift that is already open.</summary>
    public async Task<ShiftDto> OpenAsync(OpenShiftRequest request, int cashierId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var station = Guard.Required(request.Station, "Station", CheckIn.StationMaxLength);
        Guard.NotNegative(request.OpeningCash, "Opening cash");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockCashierAsync(db, cashierId, ct);
        var shift = await db.Shifts.FirstOrDefaultAsync(s => s.CashierId == cashierId && s.ClosedAt == null, ct);
        if (shift is null)
        {
            shift = new Shift { CashierId = cashierId, Station = station, OpeningCash = request.OpeningCash, OpenedAt = clock.UtcNow };
            db.Shifts.Add(shift);
            await db.SaveChangesOrConflictAsync(ct);
        }

        await transaction.CommitAsync(ct);
        return await GetAsync(shift.Id, ct);
    }

    /// <summary>Locks the cashier's staff row until the transaction ends (serializes his shift opening).</summary>
    internal static Task LockCashierAsync(GymDbContext db, int cashierId, CancellationToken ct) =>
        db.Database.ExecuteSqlAsync($"SELECT 1 FROM staff WHERE id = {cashierId} FOR UPDATE", ct);

    public async Task<ShiftDto?> GetCurrentAsync(int cashierId, CancellationToken ct = default) =>
        await Project(db.Shifts.AsNoTracking().Where(s => s.CashierId == cashierId && s.ClosedAt == null)).FirstOrDefaultAsync(ct);

    public async Task<ShiftDto> GetAsync(int id, CancellationToken ct = default) =>
        await Project(db.Shifts.AsNoTracking().Where(s => s.Id == id)).SingleOrDefaultAsync(ct)
        ?? throw new NotFoundException("shift_not_found", $"Shift {id} does not exist.");

    /// <summary>Closes a shift. A cashier closes his own shift; an admin can close any.</summary>
    public async Task<ShiftSummaryDto> CloseAsync(int id, CloseShiftRequest request, int staffId, bool isAdmin, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var shift = await db.Shifts.FindAsync([id], ct) ?? throw new NotFoundException("shift_not_found", $"Shift {id} does not exist.");
        EnsureCanSee(shift.CashierId, staffId, isAdmin);
        if (request.Note is { Length: > Shift.NoteMaxLength })
        {
            throw new DomainException("validation", $"Note can be at most {Shift.NoteMaxLength} characters.");
        }

        shift.Close(request.ClosingCash, request.Note, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return await GetSummaryAsync(id, staffId, isAdmin, ct);
    }

    public async Task<ShiftSummaryDto> GetSummaryAsync(int id, int staffId, bool isAdmin, CancellationToken ct = default)
    {
        var shift = await GetAsync(id, ct);
        EnsureCanSee(shift.CashierId, staffId, isAdmin);

        var sales = await db.Sales.AsNoTracking().Where(s => s.ShiftId == id).Select(s => new { s.Total, s.Currency, s.PaidAmount }).ToListAsync(ct);
        var lines = await db.SaleLines.AsNoTracking()
            .Where(l => l.Sale!.ShiftId == id)
            .Select(l => new { l.ProductId, l.Product!.NameEn, l.Product.NameAr, l.Quantity, l.LineTotal })
            .ToListAsync(ct);
        var products = lines
            .GroupBy(l => l.ProductId)
            .Select(g => new ProductSalesDto(g.Key, g.First().NameEn, g.First().NameAr, g.Sum(l => l.Quantity), g.Sum(l => l.LineTotal)))
            .OrderByDescending(p => p.Total).ThenBy(p => p.NameEn)
            .ToList();

        var salesTotal = sales.Sum(s => s.Total);
        var expectedCash = shift.OpeningCash + salesTotal;
        var paid = CurrencyTotalsOf.From(sales.Select(s => (s.Currency, s.Total, s.PaidAmount)));
        return new ShiftSummaryDto(shift, sales.Count, salesTotal, expectedCash, shift.ClosingCash - expectedCash, products, paid);
    }

    public async Task<IReadOnlyList<ShiftDto>> ListAsync(DateOnly from, DateOnly to, int? cashierId, CancellationToken ct = default)
    {
        var (start, end) = clock.UtcRange(from, to);
        var query = db.Shifts.AsNoTracking().Where(s => s.OpenedAt >= start && s.OpenedAt < end);
        if (cashierId is { } id)
        {
            query = query.Where(s => s.CashierId == id);
        }

        return await Project(query.OrderByDescending(s => s.OpenedAt).ThenByDescending(s => s.Id)).ToListAsync(ct);
    }

    private static void EnsureCanSee(int cashierId, int staffId, bool isAdmin)
    {
        if (!isAdmin && cashierId != staffId)
        {
            throw new ForbiddenException("not_your_shift", "This is another cashier's shift.");
        }
    }

    private static IQueryable<ShiftDto> Project(IQueryable<Shift> query) =>
        query.Select(s => new ShiftDto(s.Id, s.CashierId, s.Cashier!.FullName, s.Station, s.OpenedAt, s.ClosedAt, s.OpeningCash, s.ClosingCash, s.Note));
}
