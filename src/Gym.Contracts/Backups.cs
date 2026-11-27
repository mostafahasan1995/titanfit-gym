namespace Gym.Contracts;

/// <summary>What started a backup run.</summary>
public enum BackupTrigger
{
    /// <summary>The daily job on the server.</summary>
    Scheduled,

    /// <summary>"Back up now" in the Admin app.</summary>
    Manual,
}

/// <summary>Why the Admin app should warn about the backups.</summary>
public enum BackupWarning
{
    None,

    /// <summary>No backup has worked yet.</summary>
    NeverRun,

    /// <summary>The last run failed (drive missing, disk full, database down...).</summary>
    LastFailed,

    /// <summary>The last good backup is older than 2 days.</summary>
    TooOld,
}

/// <summary>
/// The daily backup settings (stored on the server PC, not in the database). <see cref="Folder"/> is the folder in use;
/// <see cref="IsDefaultFolder"/> means none was chosen (<see cref="DefaultFolder"/>, <c>%ProgramData%\TitanFit\backups</c>).
/// </summary>
public sealed record BackupSettingsDto(
    string Folder,
    bool IsDefaultFolder,
    string DefaultFolder,
    TimeOnly Time,
    int KeepLast,
    string? PgDumpPath,
    string? ResolvedPgDumpPath);

/// <summary>
/// Saves the backup settings. An empty <see cref="Folder"/> uses the default folder; any other folder must exist and be
/// writable by the server (codes <c>backup_folder_missing</c>, <c>backup_folder_not_writable</c>). An empty
/// <see cref="PgDumpPath"/> uses the pg_dump installed with the server.
/// </summary>
public sealed record SaveBackupSettingsRequest(string? Folder, TimeOnly Time, int KeepLast, string? PgDumpPath = null);

/// <summary>A backup file in the backup folder (<c>titanfit-YYYYMMDD-HHmm.dump</c>).</summary>
public sealed record BackupFileDto(string Name, long SizeBytes, DateTime CreatedAt);

/// <summary>
/// One backup run (times in UTC). When it failed, <see cref="ErrorCode"/> says why (<c>backup_folder_missing</c>,
/// <c>backup_folder_not_writable</c>, <c>pg_dump_not_found</c>, <c>pg_dump_failed</c>, <c>backup_timeout</c>,
/// <c>backup_failed</c>) and <see cref="Error"/> has the details in English.
/// </summary>
public sealed record BackupRunDto(
    DateTime StartedAt,
    DateTime FinishedAt,
    bool Succeeded,
    BackupTrigger Trigger,
    string? FileName,
    long? SizeBytes,
    string? ErrorCode,
    string? Error);

/// <summary>The state of the daily backup: last run, last good one, next run (UTC) and the warning to show.</summary>
public sealed record BackupStatusDto(
    string Folder,
    TimeOnly Time,
    int KeepLast,
    BackupRunDto? LastRun,
    BackupRunDto? LastSuccess,
    DateTime NextRunAt,
    BackupWarning Warning,
    bool IsRunning);
