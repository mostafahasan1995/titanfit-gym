namespace Gym.Admin.Logic;

/// <summary>The period buttons of the reports page.</summary>
public enum ReportPreset
{
    Today,

    /// <summary>From the last Saturday (the Syrian week starts on Saturday) to today.</summary>
    ThisWeek,

    /// <summary>From the 1st of this month to today.</summary>
    ThisMonth,

    /// <summary>The whole previous month.</summary>
    LastMonth,

    /// <summary>The dates the admin picked.</summary>
    Custom,
}

/// <summary>Turns a period button into the report's date range (local days, both ends included).</summary>
public static class ReportPeriod
{
    public const DayOfWeek WeekStart = DayOfWeek.Saturday;

    /// <param name="customFrom">Used for <see cref="ReportPreset.Custom"/> (default: today).</param>
    /// <param name="customTo">Used for <see cref="ReportPreset.Custom"/> (default: today).</param>
    public static (DateOnly From, DateOnly To) For(ReportPreset preset, DateOnly today, DateOnly? customFrom = null, DateOnly? customTo = null)
    {
        switch (preset)
        {
            case ReportPreset.Today:
                return (today, today);
            case ReportPreset.ThisWeek:
                return (StartOfWeek(today), today);
            case ReportPreset.ThisMonth:
                return (new DateOnly(today.Year, today.Month, 1), today);
            case ReportPreset.LastMonth:
                var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
                return (first, first.AddMonths(1).AddDays(-1));
            default:
                return (customFrom ?? today, customTo ?? today);
        }
    }

    /// <summary>The Saturday on or before <paramref name="day"/>.</summary>
    public static DateOnly StartOfWeek(DateOnly day) =>
        day.AddDays(-(((int)day.DayOfWeek - (int)WeekStart + 7) % 7));

    /// <summary>False when the end date is before the start date (the page shows an error and does not ask the server).</summary>
    public static bool IsValid(DateOnly from, DateOnly to) => to >= from;
}
