namespace Gym.Infrastructure.Services;

/// <summary>Reports over a date range (local days, both ends included).</summary>
public sealed class ReportService(GymDbContext db, GymClock clock)
{
    public async Task<SubscriptionReportDto> SubscriptionsAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var (start, end) = clock.UtcRange(from, to);
        var rows = await db.Subscriptions.AsNoTracking()
            .Where(s => s.CreatedAt >= start && s.CreatedAt < end)
            .Select(s => new { s.Kind, s.Months, s.PricePaid, s.CreatedAt, s.CreatedById, StaffName = s.CreatedBy!.FullName })
            .ToListAsync(ct);

        return new SubscriptionReportDto(
            from,
            to,
            rows.Count,
            rows.Sum(r => r.PricePaid),
            rows.GroupBy(r => (r.Kind, r.Months))
                .OrderBy(g => g.Key.Kind).ThenBy(g => g.Key.Months)
                .Select(g => new SubscriptionReportRow(g.Key.Kind, g.Key.Months, g.Count(), g.Sum(r => r.PricePaid)))
                .ToList(),
            rows.GroupBy(r => clock.ToLocalDate(r.CreatedAt))
                .OrderBy(g => g.Key)
                .Select(g => new DayAmount(g.Key, g.Count(), g.Sum(r => r.PricePaid)))
                .ToList(),
            rows.GroupBy(r => (r.CreatedById, r.StaffName))
                .Select(g => new StaffAmount(g.Key.CreatedById, g.Key.StaffName, g.Count(), g.Sum(r => r.PricePaid)))
                .OrderByDescending(s => s.Total).ThenBy(s => s.StaffName)
                .ToList());
    }

    public async Task<AttendanceReportDto> AttendanceAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var (start, end) = clock.UtcRange(from, to);
        var rows = await db.CheckIns.AsNoTracking()
            .Where(c => c.At >= start && c.At < end)
            .Select(c => new { c.MemberId, c.At, c.Result })
            .ToListAsync(ct);
        var admitted = rows.Where(r => CheckInRules.IsAdmitted(r.Result)).ToList();
        var perHour = admitted.GroupBy(r => clock.ToLocal(r.At).Hour).ToDictionary(g => g.Key, g => g.Count());

        return new AttendanceReportDto(
            from,
            to,
            rows.Count,
            admitted.Count,
            rows.Count - admitted.Count,
            admitted.Select(r => r.MemberId).Distinct().Count(),
            admitted.GroupBy(r => clock.ToLocalDate(r.At))
                .OrderBy(g => g.Key)
                .Select(g => new DayCount(g.Key, g.Count()))
                .ToList(),
            Enumerable.Range(0, 24).Select(h => new HourCount(h, perHour.GetValueOrDefault(h))).ToList());
    }

    public async Task<SalesReportDto> SalesAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var (start, end) = clock.UtcRange(from, to);
        var sales = await db.Sales.AsNoTracking()
            .Where(s => s.At >= start && s.At < end)
            .Select(s => new { s.Id, s.At, s.Total, s.CashierId, CashierName = s.Cashier!.FullName })
            .ToListAsync(ct);
        var lines = await db.SaleLines.AsNoTracking()
            .Where(l => l.Sale!.At >= start && l.Sale.At < end)
            .Select(l => new { l.ProductId, l.Product!.NameEn, l.Product.NameAr, l.Quantity, l.LineTotal })
            .ToListAsync(ct);

        return new SalesReportDto(
            from,
            to,
            sales.Count,
            sales.Sum(s => s.Total),
            sales.GroupBy(s => clock.ToLocalDate(s.At))
                .OrderBy(g => g.Key)
                .Select(g => new DayAmount(g.Key, g.Count(), g.Sum(s => s.Total)))
                .ToList(),
            lines.GroupBy(l => l.ProductId)
                .Select(g => new ProductSalesDto(g.Key, g.First().NameEn, g.First().NameAr, g.Sum(l => l.Quantity), g.Sum(l => l.LineTotal)))
                .OrderByDescending(p => p.Total).ThenBy(p => p.NameEn)
                .ToList(),
            sales.GroupBy(s => (s.CashierId, s.CashierName))
                .Select(g => new StaffAmount(g.Key.CashierId, g.Key.CashierName, g.Count(), g.Sum(s => s.Total)))
                .OrderByDescending(s => s.Total).ThenBy(s => s.StaffName)
                .ToList());
    }
}
