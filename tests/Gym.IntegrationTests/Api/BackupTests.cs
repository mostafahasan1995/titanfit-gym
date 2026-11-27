using System.Diagnostics;
using System.Net;
using System.Text;
using Gym.Client;

namespace Gym.IntegrationTests.Api;

/// <summary>
/// Backups with the real pg_dump (GYM_TEST_PG_DUMP, the PostgreSQL 17 client in CI) against the test database:
/// settings and their checks, "back up now", the file names, the "keep last N" rule and failures.
/// </summary>
public sealed class BackupTests(ApiFactory fx) : IClassFixture<ApiFactory>, IDisposable
{
    private readonly List<string> folders = [];

    [Fact]
    public async Task Settings_have_defaults_and_the_folder_is_checked_on_the_server()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var admin = await fx.AdminAsync();

        var defaults = await admin.GetBackupSettingsAsync();
        Assert.True(defaults.IsDefaultFolder);
        Assert.Equal(defaults.DefaultFolder, defaults.Folder);
        Assert.Equal("backups", Path.GetFileName(defaults.DefaultFolder));
        Assert.Equal(new TimeOnly(2, 0), defaults.Time);
        Assert.Equal(30, defaults.KeepLast);
        Assert.Null(defaults.PgDumpPath);
        Assert.NotNull(defaults.ResolvedPgDumpPath);

        var missing = Path.Combine(Path.GetTempPath(), "titanfit-no-such-drive-" + Guid.NewGuid().ToString("N"));
        Assert.Equal("backup_folder_missing", await ErrorCodeAsync(() => admin.UpdateBackupSettingsAsync(new SaveBackupSettingsRequest(missing, new TimeOnly(2, 0), 30))));
        Assert.Equal("backup_folder_invalid", await ErrorCodeAsync(() => admin.UpdateBackupSettingsAsync(new SaveBackupSettingsRequest("backups", new TimeOnly(2, 0), 30))));
        Assert.Equal("invalid_backup_keep", await ErrorCodeAsync(() => admin.UpdateBackupSettingsAsync(new SaveBackupSettingsRequest(null, new TimeOnly(2, 0), 0))));
        Assert.Equal("pg_dump_not_found", await ErrorCodeAsync(() => admin.UpdateBackupSettingsAsync(new SaveBackupSettingsRequest(null, new TimeOnly(2, 0), 30, "/no/such/pg_dump"))));

        if (!OperatingSystem.IsWindows() && Environment.UserName != "root")
        {
            var readOnly = NewFolder();
            File.SetUnixFileMode(readOnly, UnixFileMode.UserRead | UnixFileMode.UserExecute);
            Assert.Equal("backup_folder_not_writable", await ErrorCodeAsync(() => admin.UpdateBackupSettingsAsync(new SaveBackupSettingsRequest(readOnly, new TimeOnly(2, 0), 30))));
            File.SetUnixFileMode(readOnly, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        var drive = NewFolder();
        var saved = await admin.UpdateBackupSettingsAsync(new SaveBackupSettingsRequest(drive + Path.DirectorySeparatorChar, new TimeOnly(3, 30, 15), 5));
        Assert.Equal((drive, false, new TimeOnly(3, 30), 5), (saved.Folder, saved.IsDefaultFolder, saved.Time, saved.KeepLast));
        Assert.Equal(saved, await admin.GetBackupSettingsAsync());
        Assert.Empty(Directory.GetFiles(drive)); // the write test leaves nothing behind

        var status = await admin.GetBackupStatusAsync();
        Assert.Equal((drive, new TimeOnly(3, 30), 5), (status.Folder, status.Time, status.KeepLast));
        Assert.Equal(new DateTime(2026, 11, 27, 3, 30, 0, DateTimeKind.Utc), status.NextRunAt); // the test clock is at 09:00 UTC

        // Choosing the default folder (or none) goes back to the default.
        var back = await admin.UpdateBackupSettingsAsync(new SaveBackupSettingsRequest(defaults.DefaultFolder, new TimeOnly(2, 0), 30));
        Assert.True(back.IsDefaultFolder);

        var reception = await fx.ReceptionAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await Assert.ThrowsAsync<GymApiException>(() => reception.GetBackupSettingsAsync())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Assert.ThrowsAsync<GymApiException>(() => reception.RunBackupAsync())).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Assert.ThrowsAsync<GymApiException>(() => fx.Anonymous().GetBackupsAsync())).StatusCode);
    }

    [Fact]
    public async Task Back_up_now_writes_a_pg_dump_file_and_keeps_only_the_newest_backups()
    {
        Assert.False(string.IsNullOrEmpty(ApiFactory.PgDump), $"Set {ApiFactory.PgDumpVariable} to a pg_dump of PostgreSQL 17.");
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var admin = await fx.AdminAsync();
        await admin.NewMemberAsync(planMonths: 1, firstName: "Backup", lastName: "Check");

        var drive = NewFolder();
        string[] old = ["titanfit-20200101-0200.dump", "titanfit-20200102-0200.dump", "titanfit-20200103-0200.dump", "titanfit-20200104-0200.dump"];
        string[] notOurs = ["notes.txt", "titanfit-manual.dump", "other.dump", "titanfit-20200101-0200.dump.partial"];
        foreach (var name in old.Concat(notOurs))
        {
            await File.WriteAllTextAsync(Path.Combine(drive, name), "old");
        }

        await admin.UpdateBackupSettingsAsync(new SaveBackupSettingsRequest(drive, new TimeOnly(2, 0), 3));

        var first = await admin.RunBackupAsync();
        Assert.True(first.Succeeded, first.Error);
        Assert.Equal((BackupTrigger.Manual, "titanfit-20261126-0900.dump", (string?)null), (first.Trigger, first.FileName, first.ErrorCode));
        var path = Path.Combine(drive, first.FileName!);
        var bytes = await File.ReadAllBytesAsync(path);
        Assert.Equal(bytes.Length, first.SizeBytes);
        Assert.True(bytes.Length > 1024, $"dump is only {bytes.Length} bytes");
        Assert.Equal("PGDMP", Encoding.ASCII.GetString(bytes, 0, 5)); // pg_dump custom format
        Assert.Equal(new[] { "titanfit-20200103-0200.dump", "titanfit-20200104-0200.dump", "titanfit-20261126-0900.dump" }, BackupNames(drive));

        // The same minute again gets "-2"; the oldest backups beyond 3 go, other files stay.
        var second = await admin.RunBackupAsync();
        Assert.Equal("titanfit-20261126-0900-2.dump", second.FileName);
        Assert.Equal(new[] { "titanfit-20200104-0200.dump", "titanfit-20261126-0900-2.dump", "titanfit-20261126-0900.dump" }, BackupNames(drive));
        foreach (var name in notOurs)
        {
            Assert.True(File.Exists(Path.Combine(drive, name)), $"{name} must never be deleted");
        }

        var list = await admin.GetBackupsAsync();
        Assert.Equal(new[] { "titanfit-20261126-0900-2.dump", "titanfit-20261126-0900.dump", "titanfit-20200104-0200.dump" }, list.Select(b => b.Name));
        Assert.Equal(new FileInfo(path).Length, list[1].SizeBytes);

        var status = await admin.GetBackupStatusAsync();
        Assert.Equal((BackupWarning.None, "titanfit-20261126-0900-2.dump", false), (status.Warning, status.LastSuccess?.FileName, status.IsRunning));
        Assert.Equal(status.LastRun, status.LastSuccess);

        var log = await File.ReadAllTextAsync(Path.Combine(Path.GetDirectoryName((await admin.GetBackupSettingsAsync()).DefaultFolder)!, "logs", "backup.log"));
        Assert.Contains("OK manual", log, StringComparison.Ordinal);

        // pg_restore reads it (the same tools restore it on the server PC).
        var restore = Path.Combine(Path.GetDirectoryName(ApiFactory.PgDump!)!, "pg_restore");
        if (File.Exists(restore))
        {
            var start = new ProcessStartInfo(restore) { RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("--list");
            start.ArgumentList.Add(path);
            using var process = Process.Start(start)!;
            var contents = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            Assert.Equal(0, process.ExitCode);
            Assert.Contains("TABLE public members", contents, StringComparison.Ordinal);
        }

        // Three days later without a new backup: the Admin app warns.
        fx.Time.Advance(TimeSpan.FromDays(3));
        Assert.Equal(BackupWarning.TooOld, (await admin.GetBackupStatusAsync()).Warning);
        fx.Time.Set(TestTimeProvider.DefaultStart);
        await admin.UpdateBackupSettingsAsync(new SaveBackupSettingsRequest(null, new TimeOnly(2, 0), 30));
    }

    [Fact]
    public async Task A_failed_backup_is_reported_and_leaves_no_file()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart.AddHours(1));
        var admin = await fx.AdminAsync();

        // A drive that was unplugged after it was chosen.
        var drive = NewFolder();
        await admin.UpdateBackupSettingsAsync(new SaveBackupSettingsRequest(drive, new TimeOnly(2, 0), 30));
        Directory.Delete(drive);
        var unplugged = await admin.RunBackupAsync();
        Assert.Equal((false, "backup_folder_missing", (string?)null), (unplugged.Succeeded, unplugged.ErrorCode, unplugged.FileName));
        Assert.Contains(drive, unplugged.Error, StringComparison.Ordinal);
        Assert.Equal(BackupWarning.LastFailed, (await admin.GetBackupStatusAsync()).Warning);
        Assert.Empty(await admin.GetBackupsAsync());

        // pg_dump itself fails (a program that only exits with 1).
        if (File.Exists("/bin/false"))
        {
            drive = NewFolder();
            await admin.UpdateBackupSettingsAsync(new SaveBackupSettingsRequest(drive, new TimeOnly(2, 0), 30, "/bin/false"));
            var failed = await admin.RunBackupAsync();
            Assert.Equal((false, "pg_dump_failed"), (failed.Succeeded, failed.ErrorCode));
            Assert.Empty(Directory.GetFiles(drive));
            var status = await admin.GetBackupStatusAsync();
            Assert.Equal((BackupWarning.LastFailed, false), (status.Warning, status.LastRun?.Succeeded));
        }

        await admin.UpdateBackupSettingsAsync(new SaveBackupSettingsRequest(null, new TimeOnly(2, 0), 30));
        fx.Time.Set(TestTimeProvider.DefaultStart);
    }

    public void Dispose()
    {
        foreach (var folder in folders.Where(Directory.Exists))
        {
            if (!OperatingSystem.IsWindows())
            {
                File.SetUnixFileMode(folder, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            Directory.Delete(folder, recursive: true);
        }
    }

    private string NewFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "titanfit-backup-it-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        folders.Add(folder);
        return folder;
    }

    private static string[] BackupNames(string folder) =>
        Directory.GetFiles(folder, "titanfit-2*.dump").Select(f => Path.GetFileName(f)).Order(StringComparer.Ordinal).ToArray();

    private static async Task<string?> ErrorCodeAsync(Func<Task> call)
    {
        var error = await Assert.ThrowsAsync<GymApiException>(call);
        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        return error.Code;
    }
}
