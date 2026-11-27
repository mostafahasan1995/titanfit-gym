using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Gym.Infrastructure.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Gym.Infrastructure.Backups;

/// <summary>
/// Database backups with pg_dump (custom format, <c>pg_restore</c> reads it). The daily job (Gym.Api) and
/// "Back up now" call <see cref="RunAsync"/>. The admin's settings and the last results are JSON files in the data
/// folder (<c>backup-settings.json</c>, <c>backup-state.json</c>), not in the database, so they survive a restore.
/// Each run is also written to <c>logs\backup.log</c>. One run at a time.
/// </summary>
public sealed class BackupService
{
    public const string SettingsFileName = "backup-settings.json";
    public const string StateFileName = "backup-state.json";
    public const string LogFileName = "backup.log";
    private const int MaxErrorLength = 1500;
    private const long MaxLogBytes = 1024 * 1024;

    private readonly IOptions<StorageOptions> storage;
    private readonly IOptions<BackupOptions> options;
    private readonly GymClock clock;
    private readonly ILogger<BackupService> logger;
    private readonly Func<string?> connectionString;
    private readonly SemaphoreSlim running = new(1, 1);
    private readonly Lock files = new();

    public BackupService(
        IOptions<StorageOptions> storage, IOptions<BackupOptions> options, GymClock clock, ILogger<BackupService> logger, Func<string?> connectionString)
    {
        this.storage = storage;
        this.options = options;
        this.clock = clock;
        this.logger = logger;
        this.connectionString = connectionString;
    }

    /// <summary><c>%ProgramData%\TitanFit\backups</c> (the data folder + backups).</summary>
    public string DefaultFolder => Path.Combine(storage.Value.ResolvedDataPath, "backups");

    private string DataPath => storage.Value.ResolvedDataPath;

    public BackupSettingsDto GetSettings()
    {
        var settings = Effective(LoadSettings());
        return new BackupSettingsDto(
            settings.Folder,
            settings.IsDefaultFolder,
            DefaultFolder,
            settings.Time,
            settings.KeepLast,
            settings.PgDumpPath,
            FindPgDump(settings.PgDumpPath));
    }

    /// <summary>Checks and saves the settings. A chosen folder must exist and be writable (an external drive must be connected).</summary>
    public BackupSettingsDto SaveSettings(SaveBackupSettingsRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        string? folder = null;
        if (!string.IsNullOrWhiteSpace(request.Folder))
        {
            var chosen = request.Folder.Trim();
            if (!Path.IsPathFullyQualified(chosen))
            {
                throw new DomainException("backup_folder_invalid", @"Give the full path of the folder, e.g. E:\TitanFit-Backups.");
            }

            chosen = Path.TrimEndingDirectorySeparator(Path.GetFullPath(chosen));
            if (!SameFolder(chosen, DefaultFolder))
            {
                if (!Directory.Exists(chosen))
                {
                    throw new DomainException("backup_folder_missing", $"The folder {chosen} does not exist. Connect the backup drive or create the folder first.");
                }

                EnsureWritable(chosen);
                folder = chosen;
            }
        }

        if (request.KeepLast is < BackupRetention.MinKeep or > BackupRetention.MaxKeep)
        {
            throw new DomainException("invalid_backup_keep", $"Keep {BackupRetention.MinKeep} to {BackupRetention.MaxKeep} backups.");
        }

        var pgDump = string.IsNullOrWhiteSpace(request.PgDumpPath) ? null : request.PgDumpPath.Trim();
        if (pgDump is not null && !File.Exists(pgDump))
        {
            throw new DomainException("pg_dump_not_found", $"pg_dump was not found at {pgDump}.");
        }

        var stored = new StoredSettings(folder, new TimeOnly(request.Time.Hour, request.Time.Minute), request.KeepLast, pgDump);
        lock (files)
        {
            WriteJson(Path.Combine(DataPath, SettingsFileName), stored);
        }

        logger.LogInformation("Backup settings saved: folder {Folder}, daily at {Time}, keep {Keep}.", folder ?? DefaultFolder, stored.Time, stored.KeepLast);
        return GetSettings();
    }

    public BackupStatusDto GetStatus()
    {
        var settings = Effective(LoadSettings());
        var state = LoadState();
        var next = clock.LocalToUtc(BackupSchedule.NextRun(clock.LocalNow, settings.Time));
        return new BackupStatusDto(
            settings.Folder,
            settings.Time,
            settings.KeepLast,
            state.LastRun,
            state.LastSuccess,
            next,
            BackupSchedule.Warning(clock.UtcNow, state.LastRun, state.LastSuccess),
            running.CurrentCount == 0);
    }

    /// <summary>The backups in the backup folder, newest first. Empty when the folder is not there (drive unplugged).</summary>
    public IReadOnlyList<BackupFileDto> ListBackups()
    {
        var folder = Effective(LoadSettings()).Folder;
        if (!Directory.Exists(folder))
        {
            return [];
        }

        var names = Directory.EnumerateFiles(folder, BackupFileName.SearchPattern).Select(f => Path.GetFileName(f));
        return BackupRetention.NewestFirst(names)
            .Select(name => new FileInfo(Path.Combine(folder, name)))
            .Select(file => new BackupFileDto(file.Name, file.Length, file.LastWriteTimeUtc))
            .ToList();
    }

    /// <summary>Called once when the daily job starts: a new install waits for the first scheduled time.</summary>
    public void MarkScheduleStarted()
    {
        lock (files)
        {
            var state = LoadState();
            if (state.LastScheduledRunAt is null && state.ScheduleStartedAt is null)
            {
                SaveState(state with { ScheduleStartedAt = clock.UtcNow });
            }
        }
    }

    /// <summary>True when the daily backup should run now (also a missed one after the PC was off).</summary>
    public bool IsScheduledRunDue()
    {
        var settings = Effective(LoadSettings());
        var state = LoadState();
        var last = state.LastScheduledRunAt ?? state.ScheduleStartedAt ?? clock.UtcNow;
        return BackupSchedule.IsDue(clock.LocalNow, settings.Time, clock.ToLocal(last));
    }

    /// <summary>
    /// Runs pg_dump into the backup folder, then deletes the oldest backups beyond the "keep" setting. A failure is
    /// returned (and remembered), not thrown. Throws <see cref="ConflictException"/> <c>backup_running</c> while another
    /// run is in progress.
    /// </summary>
    public async Task<BackupRunDto> RunAsync(BackupTrigger trigger, CancellationToken ct = default)
    {
        if (!await running.WaitAsync(0, ct))
        {
            throw new ConflictException("backup_running", "A backup is running right now. Try again in a moment.");
        }

        try
        {
            var started = clock.UtcNow;
            var settings = Effective(LoadSettings());
            string? fileName = null;
            long? size = null;
            string? errorCode = null;
            string? error = null;
            try
            {
                (fileName, size) = await DumpAsync(settings, ct);
                ApplyRetention(settings);
            }
            catch (BackupFailedException failed)
            {
                (errorCode, error) = (failed.Code, failed.Message);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                (errorCode, error) = ("backup_failed", ex.Message);
                logger.LogError(ex, "The backup failed.");
            }

            var run = new BackupRunDto(started, clock.UtcNow, error is null, trigger, fileName, size, errorCode, Truncate(error));
            lock (files)
            {
                var state = LoadState();
                SaveState(state with
                {
                    LastRun = run,
                    LastSuccess = run.Succeeded ? run : state.LastSuccess,
                    LastScheduledRunAt = trigger == BackupTrigger.Scheduled ? started : state.LastScheduledRunAt,
                });
            }

            Log(run, settings.Folder);
            return run;
        }
        finally
        {
            running.Release();
        }
    }

    private async Task<(string FileName, long Size)> DumpAsync(EffectiveSettings settings, CancellationToken ct)
    {
        var folder = settings.Folder;
        if (settings.IsDefaultFolder)
        {
            Directory.CreateDirectory(folder);
        }
        else if (!Directory.Exists(folder))
        {
            throw new BackupFailedException("backup_folder_missing", $"The backup folder {folder} is not there. Is the backup drive connected?");
        }

        var pgDump = FindPgDump(settings.PgDumpPath)
            ?? throw new BackupFailedException("pg_dump_not_found", "pg_dump was not found. Set its path in the backup settings.");
        var database = new NpgsqlConnectionStringBuilder(connectionString() ?? "");
        if (string.IsNullOrWhiteSpace(database.Database))
        {
            throw new BackupFailedException("backup_failed", "The server has no database connection string.");
        }

        var name = BackupFileName.NextFree(clock.LocalNow, n => File.Exists(Path.Combine(folder, n)));
        var target = Path.Combine(folder, name);
        var partial = target + ".partial";
        try
        {
            var (exitCode, errors) = await RunPgDumpAsync(pgDump, database, partial, ct);
            if (exitCode != 0)
            {
                throw new BackupFailedException("pg_dump_failed", $"pg_dump stopped with exit code {exitCode}. {errors}".Trim());
            }

            if (!File.Exists(partial))
            {
                throw new BackupFailedException("pg_dump_failed", "pg_dump wrote no file.");
            }

            File.Move(partial, target);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new BackupFailedException("backup_folder_not_writable", $"The server cannot write to {folder}: {ex.Message}");
        }
        finally
        {
            TryDelete(partial);
        }

        return (name, new FileInfo(target).Length);
    }

    private async Task<(int ExitCode, string Errors)> RunPgDumpAsync(string pgDump, NpgsqlConnectionStringBuilder database, string file, CancellationToken ct)
    {
        var start = new ProcessStartInfo(pgDump)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding = Encoding.UTF8,
        };

        // Leftover PG* variables (PGPORT, PGSSLMODE...) must not change what pg_dump does. The password goes in the
        // environment, never on the command line.
        foreach (var key in start.Environment.Keys.Where(k => k.StartsWith("PG", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            start.Environment.Remove(key);
        }

        start.Environment["PGPASSWORD"] = database.Password ?? "";
        start.Environment["PGCONNECT_TIMEOUT"] = "15";
        start.ArgumentList.Add("--format=custom");
        start.ArgumentList.Add("--no-password");
        start.ArgumentList.Add("--host=" + (string.IsNullOrWhiteSpace(database.Host) ? "localhost" : database.Host));
        start.ArgumentList.Add("--port=" + database.Port.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(database.Username))
        {
            start.ArgumentList.Add("--username=" + database.Username);
        }

        start.ArgumentList.Add("--dbname=" + database.Database);
        start.ArgumentList.Add("--file=" + file);

        using var process = Process.Start(start) ?? throw new BackupFailedException("pg_dump_failed", "pg_dump could not be started.");
        var errors = process.StandardError.ReadToEndAsync(CancellationToken.None);
        var output = process.StandardOutput.ReadToEndAsync(CancellationToken.None);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(options.Value.Timeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // It ended in the meantime.
            }

            if (ct.IsCancellationRequested)
            {
                throw;
            }

            throw new BackupFailedException("backup_timeout", $"pg_dump took longer than {options.Value.Timeout.TotalMinutes:0} minutes and was stopped.");
        }

        await output;
        return (process.ExitCode, (await errors).Trim());
    }

    private void ApplyRetention(EffectiveSettings settings)
    {
        var names = Directory.EnumerateFiles(settings.Folder, BackupFileName.SearchPattern).Select(f => Path.GetFileName(f)).ToList();
        foreach (var name in BackupRetention.SelectForDeletion(names, settings.KeepLast))
        {
            try
            {
                File.Delete(Path.Combine(settings.Folder, name));
                logger.LogInformation("Old backup deleted: {File}", name);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogWarning(ex, "Old backup {File} could not be deleted.", name);
            }
        }
    }

    private string? FindPgDump(string? chosen) =>
        PgDumpLocator.Find(chosen ?? options.Value.PgDumpPath, AppContext.BaseDirectory, Environment.GetEnvironmentVariable("PATH"));

    private EffectiveSettings Effective(StoredSettings stored)
    {
        var folder = string.IsNullOrWhiteSpace(stored.Folder) ? null : stored.Folder;
        var keep = stored.KeepLast is >= BackupRetention.MinKeep and <= BackupRetention.MaxKeep ? stored.KeepLast.Value : BackupRetention.DefaultKeep;
        return new EffectiveSettings(folder ?? DefaultFolder, folder is null, stored.Time ?? BackupSchedule.DefaultTime, keep, stored.PgDumpPath);
    }

    private StoredSettings LoadSettings()
    {
        lock (files)
        {
            return ReadJson<StoredSettings>(Path.Combine(DataPath, SettingsFileName)) ?? new StoredSettings();
        }
    }

    private BackupState LoadState() => ReadJson<BackupState>(Path.Combine(DataPath, StateFileName)) ?? new BackupState();

    private void SaveState(BackupState state) => WriteJson(Path.Combine(DataPath, StateFileName), state);

    private T? ReadJson<T>(string path)
        where T : class
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), GymJson.Options) : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "{File} could not be read; using the defaults.", path);
            return null;
        }
    }

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, GymJson.Options));
        File.Move(temp, path, overwrite: true);
    }

    private void Log(BackupRunDto run, string folder)
    {
        var local = clock.ToLocal(run.StartedAt).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var trigger = run.Trigger == BackupTrigger.Scheduled ? "daily" : "manual";
        var line = run.Succeeded
            ? string.Create(CultureInfo.InvariantCulture, $"{local} OK {trigger} {Path.Combine(folder, run.FileName ?? "")} {run.SizeBytes} bytes")
            : $"{local} FAILED {trigger} {run.ErrorCode}: {run.Error}";
        if (run.Succeeded)
        {
            logger.LogInformation("Backup done: {File} ({Size} bytes).", run.FileName, run.SizeBytes);
        }
        else
        {
            logger.LogError("Backup failed ({Code}): {Error}", run.ErrorCode, run.Error);
        }

        try
        {
            var logs = Path.Combine(DataPath, "logs");
            Directory.CreateDirectory(logs);
            var path = Path.Combine(logs, LogFileName);
            if (File.Exists(path) && new FileInfo(path).Length > MaxLogBytes)
            {
                File.Move(path, path + ".old", overwrite: true);
            }

            File.AppendAllText(path, line.ReplaceLineEndings(" ") + Environment.NewLine);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "The backup log could not be written.");
        }
    }

    private static void EnsureWritable(string folder)
    {
        var probe = Path.Combine(folder, $".titanfit-write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probe, "test");
            File.Delete(probe);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new DomainException("backup_folder_not_writable", $"The server cannot write to {folder}: {ex.Message}");
        }
    }

    private static bool SameFolder(string a, string b) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(a)),
            Path.TrimEndingDirectorySeparator(Path.GetFullPath(b)),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A leftover .partial file is never mistaken for a backup.
        }
    }

    private static string? Truncate(string? text) =>
        text is { Length: > MaxErrorLength } ? text[..MaxErrorLength] + "…" : text;

    /// <summary>What backup-settings.json holds (empty = default).</summary>
    private sealed record StoredSettings(string? Folder = null, TimeOnly? Time = null, int? KeepLast = null, string? PgDumpPath = null);

    private sealed record EffectiveSettings(string Folder, bool IsDefaultFolder, TimeOnly Time, int KeepLast, string? PgDumpPath);

    /// <summary>What backup-state.json holds.</summary>
    private sealed record BackupState(
        BackupRunDto? LastRun = null,
        BackupRunDto? LastSuccess = null,
        DateTime? LastScheduledRunAt = null,
        DateTime? ScheduleStartedAt = null);

    private sealed class BackupFailedException(string code, string message) : Exception(message)
    {
        public string Code { get; } = code;
    }
}
