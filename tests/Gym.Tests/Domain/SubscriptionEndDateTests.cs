using Gym.Domain;
using static Gym.Tests.Domain.Dates;

namespace Gym.Tests.Domain;

public sealed class SubscriptionEndDateTests
{
    [Theory]
    [InlineData("2026-01-01", 1, "2026-02-01")]
    [InlineData("2026-01-15", 3, "2026-04-15")]
    [InlineData("2026-06-10", 6, "2026-12-10")]
    [InlineData("2026-11-26", 12, "2027-11-26")]
    [InlineData("2026-12-31", 1, "2027-01-31")] // across the year end
    public void End_date_adds_calendar_months(string start, int months, string expected)
    {
        Assert.Equal(D(expected), SubscriptionRules.CalculateEndDate(D(start), months));
    }

    [Theory]
    [InlineData("2027-01-31", 1, "2027-02-28")] // not a leap year
    [InlineData("2028-01-31", 1, "2028-02-29")] // leap year
    [InlineData("2028-01-30", 1, "2028-02-29")]
    [InlineData("2026-08-31", 1, "2026-09-30")]
    [InlineData("2026-03-31", 1, "2026-04-30")]
    [InlineData("2026-05-31", 6, "2026-11-30")]
    [InlineData("2027-11-30", 3, "2028-02-29")]
    [InlineData("2026-11-30", 3, "2027-02-28")]
    public void Missing_day_clamps_to_the_last_day_of_the_month(string start, int months, string expected)
    {
        Assert.Equal(D(expected), SubscriptionRules.CalculateEndDate(D(start), months));
    }

    [Theory]
    [InlineData("2028-02-29", 12, "2029-02-28")] // leap day + 1 year
    [InlineData("2028-02-29", 1, "2028-03-29")]
    [InlineData("2028-02-29", 48, "2032-02-29")] // next leap year keeps the 29th
    [InlineData("2027-02-28", 12, "2028-02-28")]
    public void Leap_day_start(string start, int months, string expected)
    {
        Assert.Equal(D(expected), SubscriptionRules.CalculateEndDate(D(start), months));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Less_than_one_month_is_rejected(int months)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SubscriptionRules.CalculateEndDate(D("2026-01-01"), months));
    }
}
