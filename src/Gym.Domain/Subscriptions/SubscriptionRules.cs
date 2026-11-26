namespace Gym.Domain;

/// <summary>
/// Subscription dates and status (docs/PLAN.md, "Business rules"). The only place these rules live:
/// check-in, search, the dashboard and the reports all call <see cref="GetStatus"/>.
/// </summary>
public static class SubscriptionRules
{
    /// <summary>Default for the "expiring soon" threshold in days. The live value is a setting.</summary>
    public const int DefaultExpiringSoonDays = 3;

    /// <summary>
    /// <c>EndDate = StartDate.AddMonths(months)</c>, by calendar months. A missing day clamps to the
    /// month's last day (31 Jan + 1 month = 28 Feb, or 29 Feb in a leap year). The end date is inclusive.
    /// </summary>
    public static DateOnly CalculateEndDate(DateOnly startDate, int months)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(months, 1);
        return startDate.AddMonths(months);
    }

    /// <summary>
    /// <list type="bullet">
    /// <item><c>Expired</c> when <c>today &gt; endDate</c> (overdue days = <c>today - endDate</c>).</item>
    /// <item><c>ExpiringSoon</c> when 0 to <paramref name="expiringSoonDays"/> days are left. The end date itself is still valid ("expires today").</item>
    /// <item><c>Active</c> otherwise.</item>
    /// </list>
    /// </summary>
    public static SubscriptionState GetStatus(DateOnly endDate, DateOnly today, int expiringSoonDays = DefaultExpiringSoonDays)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(expiringSoonDays);

        var daysLeft = endDate.DayNumber - today.DayNumber;
        if (daysLeft < 0)
        {
            return new SubscriptionState(SubscriptionStatus.Expired, 0, -daysLeft);
        }

        return daysLeft <= expiringSoonDays
            ? new SubscriptionState(SubscriptionStatus.ExpiringSoon, daysLeft, 0)
            : new SubscriptionState(SubscriptionStatus.Active, daysLeft, 0);
    }

    /// <summary>
    /// Start date of a new subscription (owner's decision on open item 5, 2026-11-26).
    /// While the current period is still valid (including its last day), the new one starts the day after
    /// the current end date, so no paid day is lost and none is counted twice. After it expired (or when
    /// there was none), the new one starts today.
    /// </summary>
    /// <param name="currentEndDate">The latest end date of the same kind of subscription, or null.</param>
    public static DateOnly GetRenewalStartDate(DateOnly? currentEndDate, DateOnly today) =>
        currentEndDate is { } end && end >= today ? end.AddDays(1) : today;
}
