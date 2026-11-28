using Gym.Domain;
using static Gym.Tests.Domain.Dates;

namespace Gym.Tests.Domain;

public sealed class RenewalTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void First_subscription_starts_today()
    {
        Assert.Equal(D("2026-11-26"), SubscriptionRules.GetRenewalStartDate(null, D("2026-11-26")));
    }

    [Fact]
    public void Early_renewal_starts_the_day_after_the_old_end_date()
    {
        Assert.Equal(D("2026-12-11"), SubscriptionRules.GetRenewalStartDate(D("2026-12-10"), D("2026-11-26")));
    }

    [Fact]
    public void Renewal_on_the_last_valid_day_starts_tomorrow()
    {
        Assert.Equal(D("2026-11-27"), SubscriptionRules.GetRenewalStartDate(D("2026-11-26"), D("2026-11-26")));
    }

    [Theory]
    [InlineData("2026-11-25")] // expired yesterday
    [InlineData("2026-01-01")] // expired long ago
    public void Renewal_after_expiry_starts_today(string oldEnd)
    {
        Assert.Equal(D("2026-11-26"), SubscriptionRules.GetRenewalStartDate(D(oldEnd), D("2026-11-26")));
    }

    [Fact]
    public void Early_renewal_loses_no_day_and_counts_none_twice()
    {
        var first = Subscription.Start(Guid.NewGuid(), SubscriptionKind.Membership, 1, 20m, null, D("2026-11-26"), 1, Now);
        var renewal = Subscription.Start(first.MemberId, SubscriptionKind.Membership, 1, 20m, first.EndDate, D("2026-12-20"), 1, Now);

        Assert.Equal(D("2026-11-26"), first.StartDate);
        Assert.Equal(D("2026-12-26"), first.EndDate);
        Assert.Equal(first.EndDate.AddDays(1), renewal.StartDate); // contiguous: no gap, no overlap
        Assert.Equal(D("2027-01-27"), renewal.EndDate);
    }

    [Fact]
    public void Renewal_after_a_month_end_chain_clamps_from_the_new_start()
    {
        // Old period ends 31 Jan 2028; the new one starts 1 Feb and runs one calendar month.
        var renewal = Subscription.Start(Guid.NewGuid(), SubscriptionKind.Membership, 1, 1, D("2028-01-31"), D("2028-01-20"), 1, Now);

        Assert.Equal(D("2028-02-01"), renewal.StartDate);
        Assert.Equal(D("2028-03-01"), renewal.EndDate);
    }

    [Fact]
    public void Start_copies_price_plan_and_author()
    {
        var memberId = Guid.NewGuid();

        var subscription = Subscription.Start(memberId, SubscriptionKind.Membership, 3, 55m, null, D("2026-11-26"), 7, Now, planId: 2);

        Assert.Equal(memberId, subscription.MemberId);
        Assert.Equal(SubscriptionKind.Membership, subscription.Kind);
        Assert.Equal(2, subscription.PlanId);
        Assert.Equal(3, subscription.Months);
        Assert.Equal(55m, subscription.PricePaid);
        Assert.Equal((Currency.Usd, 55m, (decimal?)null), (subscription.Currency, subscription.PaidAmount, subscription.ExchangeRate));
        Assert.Equal(7, subscription.CreatedById);
        Assert.Equal(Now, subscription.CreatedAt);
        Assert.Equal(D("2027-02-26"), subscription.EndDate);
    }

    [Fact]
    public void Start_rejects_zero_months_and_negative_price()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Subscription.Start(Guid.NewGuid(), SubscriptionKind.Membership, 0, 1, null, D("2026-11-26"), 1, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Subscription.Start(Guid.NewGuid(), SubscriptionKind.Membership, 1, -1, null, D("2026-11-26"), 1, Now));
    }
}
