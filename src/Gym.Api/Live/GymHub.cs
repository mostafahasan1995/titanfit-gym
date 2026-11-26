using Microsoft.AspNetCore.SignalR;

namespace Gym.Api.Live;

/// <summary>
/// Live updates for every logged-in station (<see cref="GymHubRoute.Path"/>). Stations only listen; the server
/// pushes check-ins, member changes, stock changes, product changes, sales and settings.
/// </summary>
public sealed class GymHub : Hub<IGymHubClient>
{
}
