using System.Globalization;
using Gym.UI;

namespace Gym.Admin.Logic;

/// <summary>File sizes: "3.4 MB" / "3.4 ميغابايت".</summary>
public static class SizeText
{
    public static LocalText Megabytes { get; } = new("{0} MB", "{0} ميغابايت");

    public static LocalText Kilobytes { get; } = new("{0} KB", "{0} كيلوبايت");

    public static LocalText Bytes { get; } = new("{0} bytes", "{0} بايت");

    public static string Format(long bytes, AppLanguage language) => bytes switch
    {
        >= 1024 * 1024 => Megabytes.Format(language, (bytes / (1024.0 * 1024)).ToString(bytes >= 100L * 1024 * 1024 ? "0" : "0.0", CultureInfo.InvariantCulture)),
        >= 1024 => Kilobytes.Format(language, (bytes / 1024.0).ToString("0", CultureInfo.InvariantCulture)),
        _ => Bytes.Format(language, bytes.ToString(CultureInfo.InvariantCulture)),
    };
}

/// <summary>The daily backup times offered in the settings: every half hour.</summary>
public static class BackupTimes
{
    /// <summary>00:00, 00:30 ... 23:30, plus <paramref name="current"/> when it is not on a half hour.</summary>
    public static IReadOnlyList<TimeOnly> Choices(TimeOnly current)
    {
        var times = Enumerable.Range(0, 48).Select(i => new TimeOnly(i / 2, (i % 2) * 30)).ToList();
        var minute = new TimeOnly(current.Hour, current.Minute);
        if (!times.Contains(minute))
        {
            times.Add(minute);
            times.Sort();
        }

        return times;
    }

    public static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
}
