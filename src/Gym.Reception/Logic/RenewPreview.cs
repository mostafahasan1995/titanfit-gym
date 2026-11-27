using Gym.Domain;

namespace Gym.Reception.Logic;

/// <summary>The dates a new membership or private training period would get (shown before confirming).</summary>
/// <param name="Start">First valid day.</param>
/// <param name="End">Last valid day (inclusive).</param>
/// <param name="ContinuesCurrent">True when it starts the day after the current period (early renewal), false when it starts today.</param>
public readonly record struct PeriodPreview(DateOnly Start, DateOnly End, bool ContinuesCurrent);

/// <summary>The server's renewal rule (docs/PLAN.md, "Renewal"), for the preview on screen. The server decides the real dates.</summary>
public static class RenewPreview
{
    /// <param name="currentEnd">The latest end date of the same kind of subscription, or null for the first one.</param>
    public static PeriodPreview For(DateOnly? currentEnd, DateOnly today, int months)
    {
        var start = SubscriptionRules.GetRenewalStartDate(currentEnd, today);
        return new PeriodPreview(start, SubscriptionRules.CalculateEndDate(start, months), start != today);
    }

    /// <summary>The part of the period used up to and including today, 0 to 1 (for the progress bar on the check-in card).</summary>
    public static double UsedFraction(DateOnly start, DateOnly end, DateOnly today)
    {
        var total = end.DayNumber - start.DayNumber + 1;
        if (total <= 0)
        {
            return 1;
        }

        var used = today.DayNumber - start.DayNumber + 1;
        return Math.Clamp((double)used / total, 0, 1);
    }
}
