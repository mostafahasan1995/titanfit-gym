using Gym.Domain;
using static Gym.Tests.Domain.Dates;

namespace Gym.Tests.Domain;

public sealed class SubscriptionStatusTests
{
    [Fact]
    public void Expires_today_is_still_valid_with_zero_days_left()
    {
        var state = SubscriptionRules.GetStatus(D("2026-11-26"), today: D("2026-11-26"));

        Assert.Equal(new SubscriptionState(SubscriptionStatus.ExpiringSoon, 0, 0), state);
        Assert.True(state.IsValid);
    }

    [Fact]
    public void Day_after_the_end_date_is_expired_one_day()
    {
        var state = SubscriptionRules.GetStatus(D("2026-11-26"), today: D("2026-11-27"));

        Assert.Equal(new SubscriptionState(SubscriptionStatus.Expired, 0, 1), state);
        Assert.False(state.IsValid);
    }

    [Theory]
    [InlineData("2026-11-26", "2026-11-26", SubscriptionStatus.ExpiringSoon, 0)]
    [InlineData("2026-11-27", "2026-11-26", SubscriptionStatus.ExpiringSoon, 1)]
    [InlineData("2026-11-29", "2026-11-26", SubscriptionStatus.ExpiringSoon, 3)] // exactly the threshold
    [InlineData("2026-11-30", "2026-11-26", SubscriptionStatus.Active, 4)] // one past the threshold
    [InlineData("2027-11-26", "2026-11-26", SubscriptionStatus.Active, 365)]
    public void Default_threshold_is_three_days(string end, string today, SubscriptionStatus status, int daysLeft)
    {
        var state = SubscriptionRules.GetStatus(D(end), D(today));

        Assert.Equal(status, state.Status);
        Assert.Equal(daysLeft, state.DaysLeft);
        Assert.Equal(0, state.DaysOverdue);
    }

    [Theory]
    [InlineData("2026-11-25", "2026-11-26", 1)]
    [InlineData("2026-10-26", "2026-11-26", 31)]
    [InlineData("2026-12-31", "2027-01-02", 2)] // across the year end
    [InlineData("2028-02-28", "2028-03-01", 2)] // across a leap day
    [InlineData("2027-02-28", "2027-03-01", 1)] // no leap day
    public void Overdue_days_are_today_minus_end_date(string end, string today, int overdue)
    {
        var state = SubscriptionRules.GetStatus(D(end), D(today));

        Assert.Equal(SubscriptionStatus.Expired, state.Status);
        Assert.Equal(overdue, state.DaysOverdue);
        Assert.Equal(0, state.DaysLeft);
    }

    [Theory]
    [InlineData(0, "2026-11-26", SubscriptionStatus.ExpiringSoon)] // threshold 0: only "expires today"
    [InlineData(0, "2026-11-27", SubscriptionStatus.Active)]
    [InlineData(7, "2026-12-03", SubscriptionStatus.ExpiringSoon)]
    [InlineData(7, "2026-12-04", SubscriptionStatus.Active)]
    public void Threshold_is_configurable(int threshold, string end, SubscriptionStatus status)
    {
        Assert.Equal(status, SubscriptionRules.GetStatus(D(end), D("2026-11-26"), threshold).Status);
    }

    [Fact]
    public void Negative_threshold_is_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SubscriptionRules.GetStatus(D("2026-11-26"), D("2026-11-26"), -1));
    }

    [Fact]
    public void Leap_day_end_date_counts_the_29th()
    {
        Assert.Equal(1, SubscriptionRules.GetStatus(D("2028-02-29"), D("2028-02-28")).DaysLeft);
        Assert.Equal(SubscriptionStatus.ExpiringSoon, SubscriptionRules.GetStatus(D("2028-02-29"), D("2028-02-29")).Status);
        Assert.Equal(1, SubscriptionRules.GetStatus(D("2028-02-29"), D("2028-03-01")).DaysOverdue);
    }

    [Fact]
    public void Subscription_get_status_uses_its_end_date()
    {
        var subscription = new Subscription { StartDate = D("2026-11-01"), EndDate = D("2026-12-01") };

        Assert.Equal(SubscriptionStatus.Active, subscription.GetStatus(D("2026-11-26")).Status);
        Assert.Equal(SubscriptionStatus.ExpiringSoon, subscription.GetStatus(D("2026-11-26"), expiringSoonDays: 5).Status);
        Assert.Equal(SubscriptionStatus.Expired, subscription.GetStatus(D("2026-12-02")).Status);
    }
}
