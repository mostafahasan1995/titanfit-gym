namespace Gym.Domain;

/// <summary>One paid period: a membership, or a private trainer's private training service.</summary>
public class Subscription
{
    public int Id { get; set; }

    public Guid MemberId { get; set; }

    public Member? Member { get; set; }

    public SubscriptionKind Kind { get; set; }

    /// <summary>The plan used for a membership. Null for the private training service (priced from settings).</summary>
    public int? PlanId { get; set; }

    public Plan? Plan { get; set; }

    public int Months { get; set; }

    public DateOnly StartDate { get; set; }

    /// <summary>Inclusive: the member can still enter on this day.</summary>
    public DateOnly EndDate { get; set; }

    /// <summary>Whole SYP, copied at the time of payment (later price changes do not touch it).</summary>
    public long PricePaid { get; set; }

    public int CreatedById { get; set; }

    public StaffMember? CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public SubscriptionState GetStatus(DateOnly today, int expiringSoonDays = SubscriptionRules.DefaultExpiringSoonDays) =>
        SubscriptionRules.GetStatus(EndDate, today, expiringSoonDays);

    /// <summary>
    /// Creates a new period that follows <paramref name="currentEndDate"/> (see
    /// <see cref="SubscriptionRules.GetRenewalStartDate"/>), for a first subscription as well as a renewal.
    /// </summary>
    public static Subscription Start(
        Guid memberId,
        SubscriptionKind kind,
        int months,
        long price,
        DateOnly? currentEndDate,
        DateOnly today,
        int createdById,
        DateTime createdAtUtc,
        int? planId = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(months, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(price);

        var start = SubscriptionRules.GetRenewalStartDate(currentEndDate, today);
        return new Subscription
        {
            MemberId = memberId,
            Kind = kind,
            PlanId = planId,
            Months = months,
            StartDate = start,
            EndDate = SubscriptionRules.CalculateEndDate(start, months),
            PricePaid = price,
            CreatedById = createdById,
            CreatedAt = createdAtUtc,
        };
    }
}
