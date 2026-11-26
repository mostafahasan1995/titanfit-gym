using System.Security.Claims;
using Gym.Api.Auth;

namespace Gym.Api.Endpoints;

internal static class StaffEndpoints
{
    public static void MapStaffEndpoints(this RouteGroupBuilder api)
    {
        var staff = api.MapGroup("/staff").WithTags("Staff").RequireAuthorization(Policies.Admin);

        staff.MapGet("/", (StaffType? type, bool? includeInactive, StaffService service, CancellationToken ct) =>
            service.ListAsync(type, includeInactive ?? true, ct));

        staff.MapGet("/{id:int}", (int id, StaffService service, CancellationToken ct) => service.GetAsync(id, ct));

        staff.MapPost("/", async (CreateStaffRequest request, StaffService service, CancellationToken ct) =>
        {
            var created = await service.CreateAsync(request, ct);
            return Results.Created($"/api/staff/{created.Id}", created);
        });

        staff.MapPut("/{id:int}", (int id, UpdateStaffRequest request, StaffService service, CancellationToken ct) =>
            service.UpdateAsync(id, request, ct));

        staff.MapPost("/{id:int}/password", async (int id, ResetPasswordRequest request, StaffService service, CancellationToken ct) =>
        {
            await service.ResetPasswordAsync(id, request.NewPassword, ct);
            return Results.NoContent();
        });

        // Club trainers' monthly ratings (1-5 stars + note). Setting a month again replaces that month's rating.
        staff.MapGet("/{id:int}/ratings", (int id, StaffService service, CancellationToken ct) => service.GetRatingsAsync(id, ct));

        staff.MapPut("/{id:int}/ratings/{year:int}/{month:int}", (int id, int year, int month, SetRatingRequest request, ClaimsPrincipal user, StaffService service, CancellationToken ct) =>
            service.SetRatingAsync(id, year, month, request, user.StaffId(), ct));

        staff.MapGet("/ratings", (int year, int month, StaffService service, CancellationToken ct) => service.GetMonthRatingsAsync(year, month, ct));
    }
}
