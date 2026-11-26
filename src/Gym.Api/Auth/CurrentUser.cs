using System.Globalization;
using System.Security.Claims;

namespace Gym.Api.Auth;

internal static class CurrentUser
{
    public static int StaffId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(GymClaims.StaffId), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : throw new InvalidOperationException("The request has no logged-in staff member.");

    public static bool IsAdmin(this ClaimsPrincipal user) => user.IsInRole(GymRoles.Admin);
}
