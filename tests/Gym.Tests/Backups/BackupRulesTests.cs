using Gym.Client.Sample;
using Gym.Contracts;
using Gym.Domain;
using Gym.Infrastructure;
using Gym.Infrastructure.Backups;
using Gym.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gym.Tests.Backups;

/// <summary>Backup file names, the "keep last N" rule, the daily schedule (with a fake clock) and the settings checks.</summary>
public sealed class BackupRulesTests : IDisposable
{
    private static readonly DateTime Night = new(2026, 11, 27, 2, 0, 0);
    private readonly string dataPath = Path.Combine(Path.GetTempPath(), "titanfit-backup-tests-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void File_names_hold_the_local_date_and_time_and_a_sequence_for_the_same_minute()
    {
        Assert.Equal("titanfit-20261127-0200.dump", BackupFileName.Format(Night));
        Assert.Equal("titanfit-20261127-0200-2.dump", BackupFileName.Format(Night, 2));
        Assert.Equal("titanfit-20260305-1407.dump", BackupFileName.Format(new DateTime(2026, 3, 5, 14, 7, 59)));

        var taken = new HashSet<string> { "titanfit-20261127-0200.dump", "titanfit-20261127-0200-2.dump" };
        Assert.Equal("titanfit-20261127-0200-3.dump", BackupFileName.NextFree(Night, taken.Contains));
        Assert.Equal("titanfit-20261127-0201.dump", BackupFileName.NextFree(Night.AddMinutes(1), taken.Contains));

        Assert.True(BackupFileName.TryParse("titanfit-20261127-0200-3.dump", out var at, out var sequence));
        Assert.Equal((Night, 3), (at, sequence));
        Assert.True(BackupFileName.TryParse("TitanFit-20261127-0200.DUMP", out at, out sequence));
        Assert.Equal((Night, 1), (at, sequence));
        foreach (var other in new[] { "titanfit-manual.dump", "titanfit-20261127.dump", "titanfit-20261332-0200.dump", "titanfit-20261127-0200.dump.partial", "backup.dump", "titanfit-20261127-0200-0.dump", "" })
        {
            Assert.False(BackupFileName.TryParse(other, out _, out _), other);
        }
    }

    [Fact]
    public void Retention_keeps_the_newest_backups_and_never_touches_other_files()
    {
        var names = new[]
        {
            "titanfit-20261125-0200.dump", "titanfit-20261127-0200.dump", "titanfit-20261126-0200.dump",
            "titanfit-20261127-0200-2.dump", "titanfit-20261124-0200.dump", "notes.txt", "titanfit-manual.dump",
            "titanfit-20200101-0200.dump.partial",
        };

        Assert.Equal(
            new[] { "titanfit-20261125-0200.dump", "titanfit-20261124-0200.dump" },
            BackupRetention.SelectForDeletion(names, keepLast: 3));
        Assert.Empty(BackupRetention.SelectForDeletion(names, keepLast: 30));
        Assert.Equal(4, BackupRetention.SelectForDeletion(names, keepLast: 0).Count); // at least the newest one stays
        Assert.Equal(
            new[] { "titanfit-20261127-0200-2.dump", "titanfit-20261127-0200.dump", "titanfit-20261126-0200.dump", "titanfit-20261125-0200.dump", "titanfit-20261124-0200.dump" },
            BackupRetention.NewestFirst(names));
    }

    [Theory]
    [InlineData("2026-11-26 15:00", "2026-11-27 02:00")] // after today's time: tomorrow
    [InlineData("2026-11-27 01:59", "2026-11-27 02:00")] // before it: today
    [InlineData("2026-11-27 02:00", "2026-11-28 02:00")] // exactly at it: it is running now, next is tomorrow
    [InlineData("2026-12-31 23:30", "2027-01-01 02:00")]
    public void Next_run_is_the_next_daily_time(string now, string expected) =>
        Assert.Equal(DateTime.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), BackupSchedule.NextRun(DateTime.Parse(now, System.Globalization.CultureInfo.InvariantCulture), new TimeOnly(2, 0)));

    [Fact]
    public void The_daily_run_is_due_once_per_day_and_catches_up_after_the_pc_was_off()
    {
        var time = new TimeOnly(2, 0);

        // A new install at 15:00 waits for 02:00.
        var installed = new DateTime(2026, 11, 26, 15, 0, 0);
        Assert.False(BackupSchedule.IsDue(installed.AddHours(1), time, installed));
        Assert.False(BackupSchedule.IsDue(new DateTime(2026, 11, 27, 1, 59, 0), time, installed));
        Assert.True(BackupSchedule.IsDue(Night, time, installed));

        // After the 02:00 run nothing is due until the next night.
        var ran = Night.AddSeconds(5);
        Assert.False(BackupSchedule.IsDue(Night.AddMinutes(1), time, ran));
        Assert.False(BackupSchedule.IsDue(Night.AddHours(23).AddMinutes(59), time, ran));
        Assert.True(BackupSchedule.IsDue(Night.AddDays(1), time, ran));

        // The PC was off at 02:00 and starts at 08:00: the missed run happens at once.
        Assert.True(BackupSchedule.IsDue(Night.AddDays(1).AddHours(6), time, ran));

        // The admin moves the time to 23:00 on the same day: that evening's run is due at 23:00.
        Assert.False(BackupSchedule.IsDue(Night.AddHours(20), new TimeOnly(23, 0), ran));
        Assert.True(BackupSchedule.IsDue(Night.AddHours(21), new TimeOnly(23, 0), ran));
    }

    [Fact]
    public void Warning_when_the_last_run_failed_or_the_last_good_backup_is_older_than_two_days()
    {
        var now = new DateTime(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc);
        BackupRunDto Run(DateTime at, bool ok) => new(at, at.AddSeconds(5), ok, BackupTrigger.Scheduled, ok ? "titanfit-x.dump" : null, ok ? 1000 : null, ok ? null : "pg_dump_failed", ok ? null : "failed");

        var good = Run(now.AddHours(-7), ok: true);
        Assert.Equal(BackupWarning.None, BackupSchedule.Warning(now, good, good));
        Assert.Equal(BackupWarning.NeverRun, BackupSchedule.Warning(now, null, null));
        Assert.Equal(BackupWarning.LastFailed, BackupSchedule.Warning(now, Run(now.AddHours(-1), ok: false), good));
        var old = Run(now.AddDays(-2).AddMinutes(-1), ok: true);
        Assert.Equal(BackupWarning.TooOld, BackupSchedule.Warning(now, old, old));
        var fresh = Run(now.AddDays(-2).AddMinutes(1), ok: true);
        Assert.Equal(BackupWarning.None, BackupSchedule.Warning(now, fresh, fresh));
    }

    [Fact]
    public void Pg_dump_is_found_next_to_the_server_then_on_the_path()
    {
        var root = Directory.CreateDirectory(Path.Combine(dataPath, "Program Files", "TitanFit")).FullName;
        var server = Directory.CreateDirectory(Path.Combine(root, "Server")).FullName;
        var bin = Directory.CreateDirectory(Path.Combine(root, "pgsql", "bin")).FullName;
        var onPath = Directory.CreateDirectory(Path.Combine(dataPath, "tools")).FullName;
        var pathTool = Path.Combine(onPath, PgDumpLocator.ExecutableName);
        File.WriteAllText(pathTool, "");

        Assert.Equal(pathTool, PgDumpLocator.Find(null, server, onPath));
        var installed = Path.Combine(bin, PgDumpLocator.ExecutableName);
        File.WriteAllText(installed, "");
        Assert.Equal(installed, PgDumpLocator.Find(null, server, onPath));
        Assert.Equal(pathTool, PgDumpLocator.Find(pathTool, server, ""));
        Assert.Null(PgDumpLocator.Find(Path.Combine(dataPath, "missing.exe"), server, onPath)); // a chosen path must exist
        Assert.Null(PgDumpLocator.Find(null, Path.Combine(dataPath, "elsewhere"), ""));
    }

    [Fact]
    public void Settings_are_checked_saved_and_used()
    {
        var service = NewService(new DateTime(2026, 11, 26, 15, 0, 0, DateTimeKind.Utc), pgDump: Path.Combine(dataPath, "no-pg_dump.exe"));
        var defaults = service.GetSettings();
        Assert.Equal(Path.Combine(dataPath, "backups"), defaults.Folder);
        Assert.True(defaults.IsDefaultFolder);
        Assert.Equal((new TimeOnly(2, 0), 30), (defaults.Time, defaults.KeepLast));

        Assert.Equal("backup_folder_missing", Code(() => service.SaveSettings(new SaveBackupSettingsRequest(Path.Combine(dataPath, "E-drive"), new TimeOnly(2, 0), 30))));
        Assert.Equal("backup_folder_invalid", Code(() => service.SaveSettings(new SaveBackupSettingsRequest("backups", new TimeOnly(2, 0), 30))));
        Assert.Equal("invalid_backup_keep", Code(() => service.SaveSettings(new SaveBackupSettingsRequest(null, new TimeOnly(2, 0), 366))));
        Assert.Equal("pg_dump_not_found", Code(() => service.SaveSettings(new SaveBackupSettingsRequest(null, new TimeOnly(2, 0), 30, Path.Combine(dataPath, "x.exe")))));

        var drive = Directory.CreateDirectory(Path.Combine(dataPath, "E-drive")).FullName;
        var saved = service.SaveSettings(new SaveBackupSettingsRequest(drive, new TimeOnly(23, 15, 40), 7));
        Assert.Equal((drive, false, new TimeOnly(23, 15), 7), (saved.Folder, saved.IsDefaultFolder, saved.Time, saved.KeepLast));
        Assert.True(File.Exists(Path.Combine(dataPath, BackupService.SettingsFileName)));

        // A second service (server restart) reads the same settings.
        var restarted = NewService(new DateTime(2026, 11, 26, 15, 0, 0, DateTimeKind.Utc), pgDump: null);
        Assert.Equal(saved.Folder, restarted.GetSettings().Folder);
        var status = restarted.GetStatus();
        Assert.Equal(new DateTime(2026, 11, 26, 23, 15, 0, DateTimeKind.Utc), status.NextRunAt);
        Assert.Equal(BackupWarning.NeverRun, status.Warning);
    }

    [Fact]
    public async Task A_run_without_pg_dump_fails_cleanly_and_is_remembered()
    {
        var service = NewService(new DateTime(2026, 11, 26, 15, 0, 0, DateTimeKind.Utc), pgDump: Path.Combine(dataPath, "no-pg_dump.exe"));
        service.MarkScheduleStarted();
        Assert.False(service.IsScheduledRunDue()); // a new install waits for 02:00

        var run = await service.RunAsync(BackupTrigger.Manual);

        Assert.False(run.Succeeded);
        Assert.Equal("pg_dump_not_found", run.ErrorCode);
        Assert.Null(run.FileName);
        Assert.Empty(service.ListBackups());
        var status = service.GetStatus();
        Assert.Equal((BackupWarning.LastFailed, false), (status.Warning, status.IsRunning));
        Assert.Equal(run, status.LastRun);
        Assert.Contains("FAILED manual pg_dump_not_found", File.ReadAllText(Path.Combine(dataPath, "logs", BackupService.LogFileName)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Sample_backups_follow_the_keep_rule()
    {
        var api = new SampleGymApi();
        var list = await api.GetBackupsAsync();
        Assert.Equal(14, list.Count);
        Assert.Equal(BackupWarning.None, (await api.GetBackupStatusAsync()).Warning);

        await api.UpdateBackupSettingsAsync(new SaveBackupSettingsRequest(SampleGymApi.SampleBackupFolder, new TimeOnly(2, 0), 5));
        var run = await api.RunBackupAsync();
        Assert.True(run.Succeeded);
        var after = await api.GetBackupsAsync();
        Assert.Equal(5, after.Count);
        Assert.Contains(after, b => b.Name == run.FileName);

        api.FailNextBackup = true;
        Assert.False((await api.RunBackupAsync()).Succeeded);
        Assert.Equal(BackupWarning.LastFailed, (await api.GetBackupStatusAsync()).Warning);
    }

    public void Dispose()
    {
        if (Directory.Exists(dataPath))
        {
            Directory.Delete(dataPath, recursive: true);
        }
    }

    private BackupService NewService(DateTime utcNow, string? pgDump) => new(
        Options.Create(new StorageOptions { DataPath = dataPath }),
        Options.Create(new BackupOptions { PgDumpPath = pgDump }),
        new GymClock(new FixedTime(utcNow)),
        NullLogger<BackupService>.Instance,
        () => "Host=localhost;Port=5433;Database=titanfit;Username=titanfit;Password=not-used");

    private static string Code(Action action) => Assert.Throws<DomainException>(action).Code;

    private sealed class FixedTime(DateTime utcNow) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}
