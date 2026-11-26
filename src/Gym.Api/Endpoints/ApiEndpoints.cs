namespace Gym.Api.Endpoints;

internal static class ApiEndpoints
{
    /// <summary>Maps every REST endpoint under /api. Each group or endpoint states who may call it.</summary>
    public static void MapGymApi(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");
        api.MapAuthEndpoints();
        api.MapMemberEndpoints();
        api.MapCheckInEndpoints();
        api.MapPlanEndpoints();
        api.MapPrivateTrainerEndpoints();
        api.MapStaffEndpoints();
        api.MapCafeteriaEndpoints();
        api.MapAdminEndpoints();
    }
}
