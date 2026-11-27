namespace Gym.Infrastructure;

/// <summary>
/// "Now" and "today" for the business rules. Timestamps are stored in UTC; days (today, report ranges,
/// check-ins by hour) use the server's local time zone, which is the gym's.
/// </summary>
public sealed class GymClock(TimeProvider time)
{
    public DateTime UtcNow => time.GetUtcNow().UtcDateTime;

    public DateTime LocalNow => ToLocal(UtcNow);

    public DateOnly Today => DateOnly.FromDateTime(LocalNow);

    public DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), time.LocalTimeZone);

    public DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(ToLocal(utc));

    /// <summary>The UTC instant of a local time (a time skipped by daylight saving moves forward to the next valid one).</summary>
    public DateTime LocalToUtc(DateTime local)
    {
        var zone = time.LocalTimeZone;
        local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(30);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    /// <summary>The UTC instant the local <paramref name="day"/> starts.</summary>
    public DateTime StartOfDayUtc(DateOnly day)
    {
        var zone = time.LocalTimeZone;
        var local = day.ToDateTime(TimeOnly.MinValue);
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(30); // a daylight-saving gap at midnight
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, zone);
    }

    /// <summary>UTC range [start of <paramref name="from"/>, start of the day after <paramref name="to"/>).</summary>
    public (DateTime Start, DateTime End) UtcRange(DateOnly from, DateOnly to)
    {
        if (to < from)
        {
            throw new DomainException("invalid_range", "The end date is before the start date.");
        }

        return (StartOfDayUtc(from), StartOfDayUtc(to.AddDays(1)));
    }
}
