namespace Gym.IntegrationTests;

/// <summary>A settable clock in UTC for the business rules ("today"). Login tokens keep using the real time.</summary>
public sealed class TestTimeProvider(DateTimeOffset start) : TimeProvider
{
    private readonly Lock gate = new();
    private DateTimeOffset now = start;

    public static DateTimeOffset DefaultStart { get; } = new(2026, 11, 26, 9, 0, 0, TimeSpan.Zero);

    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    public DateOnly Today => DateOnly.FromDateTime(GetUtcNow().UtcDateTime);

    public override DateTimeOffset GetUtcNow()
    {
        lock (gate)
        {
            return now;
        }
    }

    public void Set(DateTimeOffset value)
    {
        lock (gate)
        {
            now = value;
        }
    }

    public void SetToday(DateOnly day, int hour = 9) => Set(new DateTimeOffset(day.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero));

    public void Advance(TimeSpan by)
    {
        lock (gate)
        {
            now += by;
        }
    }
}
