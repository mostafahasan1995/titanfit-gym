using Gym.Api.Auth;
using Gym.Infrastructure.Backups;

namespace Gym.Api.Endpoints;

internal static class BackupEndpoints
{
    /// <summary>Database backups (Admin only): status, list, back up now, settings. No restore over the API (see docs/PLAN.md).</summary>
    public static void MapBackupEndpoints(this RouteGroupBuilder api)
    {
        var backups = api.MapGroup("/backups").WithTags("Backups").RequireAuthorization(Policies.Admin);

        backups.MapGet("/", (BackupService service) => service.ListBackups());

        backups.MapGet("/status", (BackupService service) => service.GetStatus());

        // Runs pg_dump now. A failed backup is a normal answer (succeeded = false, errorCode); 409 backup_running while one runs.
        // Not tied to the request: closing the Admin app does not stop a backup half way.
        backups.MapPost("/", (BackupService service) => service.RunAsync(BackupTrigger.Manual, CancellationToken.None));

        backups.MapGet("/settings", (BackupService service) => service.GetSettings());

        // The folder must exist and be writable (backup_folder_missing, backup_folder_not_writable).
        backups.MapPut("/settings", (SaveBackupSettingsRequest request, BackupService service) => service.SaveSettings(request));
    }
}
