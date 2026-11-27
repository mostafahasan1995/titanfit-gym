using System.Globalization;
using Gym.Contracts;

namespace Gym.Admin.Logic;

/// <summary>A calendar month of a club trainer rating.</summary>
public readonly record struct RatingMonth(int Year, int Month)
{
    public static RatingMonth Of(DateOnly day) => new(day.Year, day.Month);

    public RatingMonth Previous() => Month == 1 ? new(Year - 1, 12) : new(Year, Month - 1);

    public override string ToString() => string.Create(CultureInfo.InvariantCulture, $"{Year:0000}-{Month:00}");
}

/// <summary>
/// The months the admin can rate (the last 12, this month first) and the rating a month already has. One rating per
/// trainer and month: saving a month again replaces it (the server keeps every month as the history).
/// </summary>
public static class RatingMonths
{
    public const int Count = 12;

    /// <summary>This month and the months before it, newest first.</summary>
    public static IReadOnlyList<RatingMonth> Recent(DateOnly today, int count = Count)
    {
        var months = new List<RatingMonth>(Math.Max(0, count));
        var month = RatingMonth.Of(today);
        for (var i = 0; i < count; i++)
        {
            months.Add(month);
            month = month.Previous();
        }

        return months;
    }

    /// <summary>The month the rating dialog opens on: this month.</summary>
    public static RatingMonth Default(DateOnly today) => RatingMonth.Of(today);

    /// <summary>The rating the trainer already has for <paramref name="month"/>, or null (saving then adds one).</summary>
    public static RatingDto? Find(IEnumerable<RatingDto> ratings, RatingMonth month)
    {
        ArgumentNullException.ThrowIfNull(ratings);
        return ratings.FirstOrDefault(r => r.Year == month.Year && r.Month == month.Month);
    }

    /// <summary>The history after <paramref name="saved"/>: the same month is replaced, newest month first.</summary>
    public static IReadOnlyList<RatingDto> Replace(IEnumerable<RatingDto> ratings, RatingDto saved)
    {
        ArgumentNullException.ThrowIfNull(ratings);
        ArgumentNullException.ThrowIfNull(saved);
        return ratings
            .Where(r => !(r.Year == saved.Year && r.Month == saved.Month))
            .Append(saved)
            .OrderByDescending(r => r.Year)
            .ThenByDescending(r => r.Month)
            .ToList();
    }

    /// <summary>The average stars (one decimal), or null without ratings.</summary>
    public static double? Average(IEnumerable<RatingDto> ratings)
    {
        ArgumentNullException.ThrowIfNull(ratings);
        var list = ratings.ToList();
        return list.Count == 0 ? null : Math.Round(list.Average(r => r.Stars), 1, MidpointRounding.AwayFromZero);
    }
}
