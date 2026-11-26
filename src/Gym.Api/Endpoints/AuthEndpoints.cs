using System.Security.Claims;
using Gym.Api.Auth;

namespace Gym.Api.Endpoints;

internal static class AuthEndpoints
{
    public static void MapAuthEndpoints(this RouteGroupBuilder api)
    {
        var auth = api.MapGroup("/auth").WithTags("Auth");

        auth.MapPost("/login", async (LoginRequest request, StaffService staff, TokenService tokens, CancellationToken ct) =>
        {
            var user = await staff.VerifyLoginAsync(request.Username, request.Password, ct);
            return user is null
                ? Results.Problem(
                    statusCode: StatusCodes.Status401Unauthorized,
                    title: "Wrong username or password.",
                    extensions: new Dictionary<string, object?> { ["code"] = "invalid_login" })
                : Results.Ok(tokens.CreateLogin(user));
        }).AllowAnonymous();

        auth.MapGet("/me", (ClaimsPrincipal user, StaffService staff, CancellationToken ct) => staff.GetAsync(user.StaffId(), ct))
            .RequireAuthorization(Policies.AnyStaff);

        auth.MapPost("/change-password", async (ChangePasswordRequest request, ClaimsPrincipal user, StaffService staff, CancellationToken ct) =>
        {
            await staff.ChangeOwnPasswordAsync(user.StaffId(), request.CurrentPassword, request.NewPassword, ct);
            return Results.NoContent();
        }).RequireAuthorization(Policies.AnyStaff);

        // First run only: create the first admin login (no password is shipped with the program).
        var setup = api.MapGroup("/setup").WithTags("Setup").AllowAnonymous();
        setup.MapGet("/status", async (StaffService staff, CancellationToken ct) => new SetupStatusResponse(await staff.NeedsSetupAsync(ct)));
        setup.MapPost("/admin", async (SetupAdminRequest request, StaffService staff, TokenService tokens, CancellationToken ct) =>
            tokens.CreateLogin(await staff.SetupFirstAdminAsync(request, ct)));
    }
}
