using System.Globalization;
using Gym.Contracts;

namespace Gym.Client.Sample;

/// <summary>
/// Sample backups: 14 daily backups at 02:00 on the external drive E:, the last one this morning. "Back up now" adds
/// one, and the "keep" setting drops the oldest, like the server. Nothing is written to disk.
/// </summary>
public sealed partial class SampleGymApi
{
    public const string SampleBackupFolder = @"E:\TitanFit-Backups";
    public const string SampleDefaultBackupFolder = @"C:\ProgramData\TitanFit\backups";
    public const string SamplePgDumpPath = @"C:\Program Files\TitanFit\pgsql\bin\pg_dump.exe";

    private readonly List<BackupFileDto> backups = [];
    private SaveBackupSettingsRequest backupSettings = new(SampleBackupFolder, new TimeOnly(2, 0), 30);
    private BackupRunDto? lastBackupRun;
    private BackupRunDto? lastGoodBackup;
    private bool backupsSeeded;

    /// <summary>Makes the next <see cref="RunBackupAsync"/> fail like an unplugged backup drive.</summary>
    public bool FailNextBackup { get; set; }

    public Task<BackupStatusDto> GetBackupStatusAsync(CancellationToken ct = default)
    {
        SeedBackups();
        var nowLocal = DateTime.Now;
        var next = nowLocal.Date + backupSettings.Time.ToTimeSpan();
        if (next <= nowLocal)
        {
            next = next.AddDays(1);
        }

        var warning = lastBackupRun is { Succeeded: false } ? BackupWarning.LastFailed
            : lastGoodBackup is null ? BackupWarning.NeverRun
            : DateTime.UtcNow - lastGoodBackup.FinishedAt > TimeSpan.FromDays(2) ? BackupWarning.TooOld
            : BackupWarning.None;
        return Task.FromResult(new BackupStatusDto(
            BackupFolder, backupSettings.Time, backupSettings.KeepLast, lastBackupRun, lastGoodBackup, next.ToUniversalTime(), warning, false));
    }

    public Task<IReadOnlyList<BackupFileDto>> GetBackupsAsync(CancellationToken ct = default)
    {
        SeedBackups();
        return ListOf(backups.OrderByDescending(b => b.Name, StringComparer.Ordinal));
    }

    public Task<BackupRunDto> RunBackupAsync(CancellationToken ct = default)
    {
        SeedBackups();
        var started = DateTime.UtcNow;
        BackupRunDto run;
        if (FailNextBackup)
        {
            FailNextBackup = false;
            run = new BackupRunDto(started, started.AddSeconds(1), false, BackupTrigger.Manual, null, null, "backup_folder_missing",
                $"The backup folder {BackupFolder} is not there. Is the backup drive connected?");
        }
        else
        {
            var local = started.ToLocalTime();
            var stamp = local.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture);
            var name = $"titanfit-{stamp}.dump";
            for (var sequence = 2; backups.Any(b => b.Name == name); sequence++)
            {
                name = $"titanfit-{stamp}-{sequence.ToString(CultureInfo.InvariantCulture)}.dump";
            }

            var size = (backups.Count == 0 ? 3_200_000 : backups.Max(b => b.SizeBytes)) + 4_096;
            backups.Add(new BackupFileDto(name, size, started.AddSeconds(6)));
            run = new BackupRunDto(started, started.AddSeconds(6), true, BackupTrigger.Manual, name, size, null, null);
            lastGoodBackup = run;
            ApplyBackupRetention();
        }

        lastBackupRun = run;
        return Task.FromResult(run);
    }

    public Task<BackupSettingsDto> GetBackupSettingsAsync(CancellationToken ct = default) => Task.FromResult(BackupSettings());

    public Task<BackupSettingsDto> UpdateBackupSettingsAsync(SaveBackupSettingsRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var folder = string.IsNullOrWhiteSpace(request.Folder) ? null : request.Folder.Trim();
        if (folder is not null && !Path.IsPathFullyQualified(folder))
        {
            throw Bad("backup_folder_invalid", @"Give the full path of the folder, e.g. E:\TitanFit-Backups.");
        }

        if (request.KeepLast is < 1 or > 365)
        {
            throw Bad("invalid_backup_keep", "Keep 1 to 365 backups.");
        }

        backupSettings = request with
        {
            Folder = folder,
            Time = new TimeOnly(request.Time.Hour, request.Time.Minute),
            PgDumpPath = string.IsNullOrWhiteSpace(request.PgDumpPath) ? null : request.PgDumpPath.Trim(),
        };
        ApplyBackupRetention();
        return Task.FromResult(BackupSettings());
    }

    private string BackupFolder => backupSettings.Folder ?? SampleDefaultBackupFolder;

    private BackupSettingsDto BackupSettings() => new(
        BackupFolder,
        backupSettings.Folder is null,
        SampleDefaultBackupFolder,
        backupSettings.Time,
        backupSettings.KeepLast,
        backupSettings.PgDumpPath,
        backupSettings.PgDumpPath ?? SamplePgDumpPath);

    private void ApplyBackupRetention()
    {
        var keep = backups.OrderByDescending(b => b.Name, StringComparer.Ordinal).Take(backupSettings.KeepLast).ToHashSet();
        backups.RemoveAll(b => !keep.Contains(b));
    }

    private void SeedBackups()
    {
        if (backupsSeeded)
        {
            return;
        }

        backupsSeeded = true;
        long size = 3_180_000;
        for (var daysAgo = 13; daysAgo >= 0; daysAgo--)
        {
            var day = Today.AddDays(-daysAgo);
            var started = At(day, 2, 0);
            size += 5_000 + ((daysAgo * 7_919) % 9_000);
            var name = $"titanfit-{day.ToString("yyyyMMdd", CultureInfo.InvariantCulture)}-0200.dump";
            backups.Add(new BackupFileDto(name, size, started.AddSeconds(7)));
            lastGoodBackup = new BackupRunDto(started, started.AddSeconds(7), true, BackupTrigger.Scheduled, name, size, null, null);
        }

        lastBackupRun = lastGoodBackup;
    }
}
