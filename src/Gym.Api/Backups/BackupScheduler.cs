using Gym.Infrastructure.Backups;
using Microsoft.Extensions.Options;

namespace Gym.Api.Backups;

/// <summary>
/// The daily database backup: checks once a minute whether the scheduled time (setting, default 02:00) has passed
/// without a run, also after the PC was off at that time, and then runs <see cref="BackupService.RunAsync"/>.
/// Turned off with <c>Backup:ScheduleEnabled = false</c> (tests).
/// </summary>
internal sealed class BackupScheduler(BackupService backups, IOptions<BackupOptions> options, ILogger<BackupScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.ScheduleEnabled)
        {
            logger.LogInformation("The daily backup is turned off (Backup:ScheduleEnabled = false).");
            return;
        }

        try
        {
            try
            {
                backups.MarkScheduleStarted();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                logger.LogError(ex, "The backup state file could not be written.");
            }

            await Task.Delay(options.Value.StartupDelay, stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            do
            {
                await RunIfDueAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The server is stopping.
        }
    }

    private async Task RunIfDueAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (backups.IsScheduledRunDue())
            {
                await backups.RunAsync(BackupTrigger.Scheduled, stoppingToken);
            }
        }
        catch (ConflictException)
        {
            // "Back up now" is running; the daily run follows on the next check.
        }
#pragma warning disable CA1031 // The daily job must keep running after any error; it is logged.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            logger.LogError(ex, "The daily backup check failed.");
        }
    }
}
