namespace Gym.Reception.Logic;

/// <summary>
/// The camera sees the same QR in many frames, and a member may hold the phone up for a while: only the first sighting
/// checks in. A code counts again once it has not been seen for <see cref="Window"/> (each sighting restarts the wait).
/// </summary>
public sealed class CheckInDebouncer
{
    public static readonly TimeSpan DefaultWindow = TimeSpan.FromSeconds(5);

    private readonly Dictionary<string, DateTime> lastSeen = new(StringComparer.Ordinal);

    public CheckInDebouncer(TimeSpan? window = null)
    {
        Window = window ?? DefaultWindow;
        if (Window <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(window), "The window must be positive.");
        }
    }

    public TimeSpan Window { get; }

    /// <summary>True when <paramref name="code"/> should check in now; false for a repeat within the window.</summary>
    public bool ShouldCheckIn(string code, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(code);
        var key = code.Trim().ToUpperInvariant();
        foreach (var old in lastSeen.Where(e => now - e.Value >= Window).Select(e => e.Key).ToList())
        {
            lastSeen.Remove(old);
        }

        var repeat = lastSeen.ContainsKey(key);
        lastSeen[key] = now;
        return !repeat;
    }

    /// <summary>Forgets every code (e.g. after the staff pressed "check in again" by hand).</summary>
    public void Clear() => lastSeen.Clear();
}
