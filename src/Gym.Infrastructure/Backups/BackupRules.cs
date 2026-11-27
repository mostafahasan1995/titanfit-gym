using System.Globalization;
using System.Text.RegularExpressions;

namespace Gym.Infrastructure.Backups;

/// <summary>
/// Backup file names: <c>titanfit-YYYYMMDD-HHmm.dump</c> in the server's local time, with <c>-2</c>, <c>-3</c>... when
/// that minute already has one. Only files named like this are ever deleted by the retention.
/// </summary>
public static partial class BackupFileName
{
    public const string Prefix = "titanfit-";
    public const string Extension = ".dump";

    /// <summary>For <see cref="Directory.EnumerateFiles(string, string)"/>; check each name with <see cref="TryParse"/>.</summary>
    public const string SearchPattern = "titanfit-*.dump";

    public static string Format(DateTime local, int sequence = 1) => sequence <= 1
        ? string.Create(CultureInfo.InvariantCulture, $"{Prefix}{local:yyyyMMdd-HHmm}{Extension}")
        : string.Create(CultureInfo.InvariantCulture, $"{Prefix}{local:yyyyMMdd-HHmm}-{sequence}{Extension}");

    /// <summary>The first name for <paramref name="local"/> that <paramref name="exists"/> says is free.</summary>
    public static string NextFree(DateTime local, Func<string, bool> exists)
    {
        ArgumentNullException.ThrowIfNull(exists);
        for (var sequence = 1; ; sequence++)
        {
            var name = Format(local, sequence);
            if (!exists(name))
            {
                return name;
            }
        }
    }

    /// <summary>Reads the local time and sequence (1 = no suffix) from a backup file name.</summary>
    public static bool TryParse(string? name, out DateTime local, out int sequence)
    {
        local = default;
        sequence = 0;
        var match = name is null ? null : Pattern().Match(name);
        if (match is not { Success: true }
            || !DateTime.TryParseExact(match.Groups[1].Value + match.Groups[2].Value, "yyyyMMddHHmm", CultureInfo.InvariantCulture, DateTimeStyles.None, out local))
        {
            return false;
        }

        sequence = match.Groups[3].Success ? int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture) : 1;
        return sequence >= 1;
    }

    [GeneratedRegex(@"^titanfit-(\d{8})-(\d{4})(?:-(\d{1,4}))?\.dump$", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();
}

/// <summary>Keeps the newest N backups. Files that are not named like our backups are never touched.</summary>
public static class BackupRetention
{
    public const int MinKeep = 1;
    public const int MaxKeep = 365;
    public const int DefaultKeep = 30;

    /// <summary>The backup file names to delete so that only the newest <paramref name="keepLast"/> stay.</summary>
    public static IReadOnlyList<string> SelectForDeletion(IEnumerable<string> fileNames, int keepLast)
    {
        ArgumentNullException.ThrowIfNull(fileNames);
        keepLast = Math.Max(MinKeep, keepLast);
        return fileNames
            .Select(Parse)
            .OfType<ParsedName>()
            .OrderByDescending(f => f.At)
            .ThenByDescending(f => f.Sequence)
            .ThenByDescending(f => f.Name, StringComparer.Ordinal)
            .Skip(keepLast)
            .Select(f => f.Name)
            .ToList();
    }

    /// <summary>Backup names newest first (the order of the list in the Admin app).</summary>
    public static IEnumerable<string> NewestFirst(IEnumerable<string> fileNames) =>
        fileNames.Select(Parse).OfType<ParsedName>()
            .OrderByDescending(f => f.At).ThenByDescending(f => f.Sequence).ThenByDescending(f => f.Name, StringComparer.Ordinal)
            .Select(f => f.Name);

    private static ParsedName? Parse(string name) =>
        BackupFileName.TryParse(name, out var at, out var sequence) ? new ParsedName(name, at, sequence) : null;

    private sealed record ParsedName(string Name, DateTime At, int Sequence);
}

/// <summary>When the daily backup runs, and when the Admin app warns about it.</summary>
public static class BackupSchedule
{
    /// <summary>The last good backup may be this old before the Admin app warns.</summary>
    public const int WarnAfterDays = 2;

    public static TimeOnly DefaultTime { get; } = new(2, 0);

    /// <summary>The next scheduled run strictly after <paramref name="localNow"/>.</summary>
    public static DateTime NextRun(DateTime localNow, TimeOnly time)
    {
        var today = localNow.Date + time.ToTimeSpan();
        return localNow < today ? today : today.AddDays(1);
    }

    /// <summary>The latest scheduled time at or before <paramref name="localNow"/>.</summary>
    public static DateTime LastDue(DateTime localNow, TimeOnly time)
    {
        var today = localNow.Date + time.ToTimeSpan();
        return localNow >= today ? today : today.AddDays(-1);
    }

    /// <summary>
    /// True when the daily backup should run now: nothing ran since the latest scheduled time. This also catches up a
    /// run that was missed while the PC was off. <paramref name="lastRunLocal"/> is the last scheduled run, or (before
    /// the first one) the time the server first started the schedule, so a new install waits for the first scheduled time.
    /// </summary>
    public static bool IsDue(DateTime localNow, TimeOnly time, DateTime lastRunLocal) => lastRunLocal < LastDue(localNow, time);

    public static BackupWarning Warning(DateTime utcNow, BackupRunDto? lastRun, BackupRunDto? lastSuccess, int warnAfterDays = WarnAfterDays)
    {
        if (lastRun is { Succeeded: false })
        {
            return BackupWarning.LastFailed;
        }

        if (lastSuccess is null)
        {
            return BackupWarning.NeverRun;
        }

        return utcNow - lastSuccess.FinishedAt > TimeSpan.FromDays(warnAfterDays) ? BackupWarning.TooOld : BackupWarning.None;
    }
}

/// <summary>Finds pg_dump: a chosen path, else the one installed with the server (<c>..\pgsql\bin</c>), else the PATH.</summary>
public static class PgDumpLocator
{
    public static string ExecutableName => OperatingSystem.IsWindows() ? "pg_dump.exe" : "pg_dump";

    /// <param name="chosen">A path from the settings or the configuration: used only when it exists.</param>
    /// <param name="serverFolder">The folder of Gym.Api.exe (<c>C:\Program Files\TitanFit\Server</c>).</param>
    /// <param name="pathVariable">The PATH environment variable.</param>
    public static string? Find(string? chosen, string serverFolder, string? pathVariable)
    {
        if (!string.IsNullOrWhiteSpace(chosen))
        {
            return File.Exists(chosen) ? Path.GetFullPath(chosen) : null;
        }

        var installed = Path.GetFullPath(Path.Combine(serverFolder, "..", "pgsql", "bin", ExecutableName));
        if (File.Exists(installed))
        {
            return installed;
        }

        foreach (var folder in (pathVariable ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            try
            {
                var candidate = Path.Combine(folder.Trim('"'), ExecutableName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // A broken PATH entry: skip it.
            }
        }

        return null;
    }
}

/// <summary>Server configuration of the backups (section "Backup"). The admin's choices are in backup-settings.json.</summary>
public sealed class BackupOptions
{
    public const string SectionName = "Backup";

    /// <summary>Runs the daily backup job (the tests turn it off).</summary>
    public bool ScheduleEnabled { get; set; } = true;

    /// <summary>pg_dump to use when the admin chose none (development and tests). Default: next to the server, then PATH.</summary>
    public string? PgDumpPath { get; set; }

    /// <summary>A pg_dump that takes longer is stopped and the run fails.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>Wait after the server starts before the first check (the database may still be starting).</summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromMinutes(1);
}
