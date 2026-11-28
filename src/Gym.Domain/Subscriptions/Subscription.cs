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

    /// <summary>US dollars, copied at the time of payment (later price changes do not touch it). The payment's dollar value.</summary>
    public decimal PricePaid { get; set; }

    /// <summary>The currency the customer paid in.</summary>
    public Currency Currency { get; set; } = Currency.Usd;

    /// <summary>The amount paid in <see cref="Currency"/>: the dollars, or the pounds (rounded) at <see cref="ExchangeRate"/>.</summary>
    public decimal PaidAmount { get; set; }

    /// <summary>The rate used (pounds for 1 dollar); null when no rate existed yet (a dollar payment).</summary>
    public decimal? ExchangeRate { get; set; }

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
        decimal price,
        DateOnly? currentEndDate,
        DateOnly today,
        int createdById,
        DateTime createdAtUtc,
        int? planId = null,
        PaymentAmount? payment = null)
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
            Currency = payment?.Currency ?? Currency.Usd,
            PaidAmount = payment?.Amount ?? price,
            ExchangeRate = payment?.SypPerUsd,
            CreatedById = createdById,
            CreatedAt = createdAtUtc,
        };
    }
}
