using System.Security.Claims;
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
        settings.MapPut("/", async (SettingsDto request, SettingsService service, ExchangeRateService rates, LiveUpdates live, CancellationToken ct) =>
        {
            var updated = await service.UpdateAsync(request, ct);
            await live.SettingsChangedAsync(updated);
            await live.ExchangeRateChangedAsync(await rates.GetStatusAsync(ct)); // the rounding step is part of it
            return updated;
        }).RequireAuthorization(Policies.Admin);

        // The daily exchange rate (Syrian pounds for 1 US dollar), entered by the manager. The newest one is in use.
        var rates = api.MapGroup("/exchange-rates").WithTags("Exchange rate");
        rates.MapGet("/current", (ExchangeRateService service, CancellationToken ct) => service.GetStatusAsync(ct))
            .RequireAuthorization(Policies.AnyStaff);
        rates.MapGet("/", (DateOnly? from, DateOnly? to, ExchangeRateService service, CancellationToken ct) => service.ListAsync(from, to, ct))
            .RequireAuthorization(Policies.Admin);
        rates.MapPost("/", async (SetExchangeRateRequest request, ClaimsPrincipal user, ExchangeRateService service, LiveUpdates live, CancellationToken ct) =>
        {
            var rate = await service.SetAsync(request, user.StaffId(), ct);
            await live.ExchangeRateChangedAsync(await service.GetStatusAsync(ct));
            return Results.Created($"/api/exchange-rates/{rate.Id}", rate);
        }).RequireAuthorization(Policies.Admin);
    }
}
