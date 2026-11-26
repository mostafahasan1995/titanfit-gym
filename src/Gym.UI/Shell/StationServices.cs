using System.Net.Http;
using Gym.Client;
using Gym.Client.Sample;
using Gym.UI.Station;

namespace Gym.UI.Shell;

/// <summary>Where a station gets its settings, API and live connection: the real server, or the in-memory sample.</summary>
public sealed class StationServices
{
    public required StationSettingsStore Store { get; init; }

    public required Func<StationSettings, IGymApi> CreateApi { get; init; }

    public required Func<IGymApi, ILiveConnection> CreateLive { get; init; }

    /// <summary>Calls <c>GET /health</c> on a server address (the "Test connection" button).</summary>
    public required Func<Uri, CancellationToken, Task<HealthResponse>> TestServerAsync { get; init; }

    /// <summary>The smoke screenshots: sample data, nothing saved, no hardware.</summary>
    public bool IsSample { get; init; }

    /// <summary>The normal run: <c>%ProgramData%\TitanFit\station.json</c> and the real server.</summary>
    public static StationServices ForServer(string? settingsPath = null) => new()
    {
        Store = new StationSettingsStore(settingsPath ?? StationSettingsStore.DefaultPath),
        CreateApi = settings => new GymApiClient(new HttpClient { BaseAddress = settings.ServerUri, Timeout = TimeSpan.FromSeconds(20) }),
        CreateLive = api => new GymLiveConnection(
            api.BaseAddress ?? new Uri(StationSettings.DefaultServerUrl + "/"),
            () => Task.FromResult(api.AccessToken)),
        TestServerAsync = async (server, ct) =>
        {
            using var http = new HttpClient { BaseAddress = server, Timeout = TimeSpan.FromSeconds(6) };
            return await new GymApiClient(http).GetHealthAsync(ct).ConfigureAwait(false);
        },
    };

    /// <summary>The smoke screenshots: one <see cref="SampleGymApi"/> for everything, settings in a temp file never written.</summary>
    public static StationServices ForSample(SampleGymApi api)
    {
        ArgumentNullException.ThrowIfNull(api);
        return new StationServices
        {
            Store = new StationSettingsStore(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "titanfit-sample-station.json")),
            CreateApi = _ => api,
            CreateLive = _ => api.Live,
            TestServerAsync = (_, ct) => api.GetHealthAsync(ct),
            IsSample = true,
        };
    }
}
