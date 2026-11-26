using Gym.Api.Auth;
using Gym.Api.Live;

namespace Gym.Api.Endpoints;

internal static class AdminEndpoints
{
    public static void MapAdminEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/dashboard", (DashboardService service, CancellationToken ct) => service.GetAsync(ct))
            .WithTags("Admin").RequireAuthorization(Policies.Admin);

        // Date ranges are local days, both ends included (yyyy-MM-dd).
        var reports = api.MapGroup("/reports").WithTags("Reports").RequireAuthorization(Policies.Admin);
        reports.MapGet("/subscriptions", (DateOnly from, DateOnly to, ReportService service, CancellationToken ct) => service.SubscriptionsAsync(from, to, ct));
        reports.MapGet("/attendance", (DateOnly from, DateOnly to, ReportService service, CancellationToken ct) => service.AttendanceAsync(from, to, ct));
        reports.MapGet("/sales", (DateOnly from, DateOnly to, ReportService service, CancellationToken ct) => service.SalesAsync(from, to, ct));

        var settings = api.MapGroup("/settings").WithTags("Settings");
        settings.MapGet("/", (SettingsService service, CancellationToken ct) => service.GetDtoAsync(ct)).RequireAuthorization(Policies.AnyStaff);
        settings.MapPut("/", async (SettingsDto request, SettingsService service, LiveUpdates live, CancellationToken ct) =>
        {
            var updated = await service.UpdateAsync(request, ct);
            await live.SettingsChangedAsync(updated);
            return updated;
        }).RequireAuthorization(Policies.Admin);
    }
}
