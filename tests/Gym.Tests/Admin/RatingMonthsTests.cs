using Gym.Admin.Logic;
using Gym.Contracts;

namespace Gym.Tests.Admin;

/// <summary>The rating dialog: the last 12 months (this month first) and one rating per month that a new one replaces.</summary>
public sealed class RatingMonthsTests
{
    [Fact]
    public void Recent_months_go_back_twelve_months_across_the_year_end()
    {
        var months = RatingMonths.Recent(new DateOnly(2027, 2, 10));

        Assert.Equal(12, months.Count);
        Assert.Equal(new RatingMonth(2027, 2), months[0]);
        Assert.Equal(new RatingMonth(2027, 1), months[1]);
        Assert.Equal(new RatingMonth(2026, 12), months[2]);
        Assert.Equal(new RatingMonth(2026, 3), months[11]);
        Assert.Equal(months.Count, months.Distinct().Count());
        Assert.Equal(new RatingMonth(2027, 2), RatingMonths.Default(new DateOnly(2027, 2, 28)));
        Assert.Equal(new RatingMonth(2026, 12), new RatingMonth(2027, 1).Previous());
        Assert.Equal("2026-03", new RatingMonth(2026, 3).ToString());
    }

    [Fact]
    public void A_month_with_a_rating_is_found_and_saving_it_again_replaces_it()
    {
        var november = Rating(1, 2026, 11, stars: 3, "Late twice");
        var october = Rating(2, 2026, 10, stars: 4, null);
        IReadOnlyList<RatingDto> history = [november, october];

        Assert.Same(november, RatingMonths.Find(history, new RatingMonth(2026, 11)));
        Assert.Null(RatingMonths.Find(history, new RatingMonth(2026, 12)));
        Assert.Null(RatingMonths.Find(history, new RatingMonth(2025, 11))); // same month, other year

        var again = Rating(1, 2026, 11, stars: 5, "Much better");
        var replaced = RatingMonths.Replace(history, again);
        Assert.Equal(2, replaced.Count);
        Assert.Equal((5, "Much better"), (replaced[0].Stars, replaced[0].Note));
        Assert.Equal(october, replaced[1]);

        var added = RatingMonths.Replace(replaced, Rating(3, 2026, 12, stars: 4, null));
        Assert.Equal(new[] { (2026, 12), (2026, 11), (2026, 10) }, added.Select(r => (r.Year, r.Month)));
    }

    [Fact]
    public void Average_has_one_decimal_and_is_null_without_ratings()
    {
        Assert.Null(RatingMonths.Average([]));
        Assert.Equal(4.5, RatingMonths.Average([Rating(1, 2026, 11, 4, null), Rating(2, 2026, 10, 5, null)]));
        Assert.Equal(4.3, RatingMonths.Average([Rating(1, 2026, 11, 4, null), Rating(2, 2026, 10, 5, null), Rating(3, 2026, 9, 4, null)]));
    }

    private static RatingDto Rating(int id, int year, int month, int stars, string? note) =>
        new(id, 6, "سامر حداد", year, month, stars, note, "خالد الأحمد", new DateTime(year, month, 28, 10, 0, 0, DateTimeKind.Utc));
}
