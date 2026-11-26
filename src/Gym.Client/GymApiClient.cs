using System.Net.Http.Json;

namespace Gym.Client;

/// <summary>Typed client for the Gym.Api server. The apps talk only to the API, never to the database.</summary>
public sealed class GymApiClient(HttpClient http)
{
    /// <summary>Calls <c>GET /health</c>.</summary>
    public async Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var health = await http.GetFromJsonAsync<HealthResponse>("health", cancellationToken).ConfigureAwait(false);
        return health ?? throw new InvalidOperationException("The server returned an empty health response.");
    }
}

/// <summary>Body of <c>GET /health</c>.</summary>
public sealed record HealthResponse(string Status, string Version);
