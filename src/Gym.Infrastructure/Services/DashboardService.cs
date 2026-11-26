namespace Gym.Infrastructure.Services;

/// <summary>The admin dashboard: today's check-ins, member counts, revenue, low stock and who expires soon.</summary>
public sealed class DashboardService(GymDbContext db, GymClock clock, SettingsService settings, StockService stock)
{
    public const int ExpiringListSize = 50;

    public async Task<DashboardDto> GetAsync(CancellationToken ct = default)
    {
        var today = clock.Today;
        var threshold = await settings.GetExpiringSoonDaysAsync(ct);
        var (start, end) = clock.UtcRange(today, today);

        var checkIns = await db.CheckIns.AsNoTracking()
            .Where(c => c.At >= start && c.At < end)
            .Select(c => new { c.At, c.Result })
            .ToListAsync(ct);
        var admitted = checkIns.Where(c => CheckInRules.IsAdmitted(c.Result)).ToList();
        var perHour = admitted.GroupBy(c => clock.ToLocal(c.At).Hour).ToDictionary(g => g.Key, g => g.Count());
        var byHour = Enumerable.Range(0, 24).Select(h => new HourCount(h, perHour.GetValueOrDefault(h))).ToList();

        var members = db.Members.AsNoTracking();
        var active = await members.WhereMembershipStatus(SubscriptionStatus.Active, today, threshold).CountAsync(ct);
        var expiring = await members.WhereMembershipStatus(SubscriptionStatus.ExpiringSoon, today, threshold).CountAsync(ct);
        var expired = await members.WhereMembershipStatus(SubscriptionStatus.Expired, today, threshold).CountAsync(ct);

        var payments = await db.Subscriptions.AsNoTracking()
            .Where(s => s.CreatedAt >= start && s.CreatedAt < end)
            .Select(s => new { s.Kind, s.PricePaid })
            .ToListAsync(ct);
        var revenueSubscriptions = payments.Where(p => p.Kind == SubscriptionKind.Membership).Sum(p => p.PricePaid);
        var revenuePrivateTraining = payments.Where(p => p.Kind == SubscriptionKind.PrivateTraining).Sum(p => p.PricePaid);
        var revenueCafeteria = (await db.Sales.AsNoTracking()
            .Where(s => s.At >= start && s.At < end)
            .Select(s => s.Total)
            .ToListAsync(ct)).Sum();

        var lowStock = await stock.GetLevelsAsync(null, lowOnly: true, ct);
        var expiringList = await members
            .WhereMembershipStatus(SubscriptionStatus.ExpiringSoon, today, threshold)
            .OrderBy(m => m.Subscriptions.Where(s => s.Kind == SubscriptionKind.Membership).Max(s => (DateOnly?)s.EndDate))
            .ThenBy(m => m.MemberNo)
            .Take(ExpiringListSize)
            .ToSummariesAsync(today, threshold, ct);

        return new DashboardDto(
            today,
            admitted.Count,
            checkIns.Count - admitted.Count,
            byHour,
            active,
            expiring,
            expired,
            revenueSubscriptions,
            revenuePrivateTraining,
            revenueCafeteria,
            revenueSubscriptions + revenuePrivateTraining + revenueCafeteria,
            lowStock,
            expiringList);
    }
}
