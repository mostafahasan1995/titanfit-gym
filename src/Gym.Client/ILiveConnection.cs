using Gym.Contracts;

namespace Gym.Client;

/// <summary>State of the live-updates connection, for the status light in the app header.</summary>
public enum LiveStatus
{
    /// <summary>Not connected (server not reachable, or not started).</summary>
    Offline,

    /// <summary>First connection attempt in progress.</summary>
    Connecting,

    Connected,

    /// <summary>The connection dropped; trying to get it back.</summary>
    Reconnecting,
}

/// <summary>
/// The server's live updates (SignalR). <see cref="GymLiveConnection"/> is the real one;
/// <see cref="Sample.SampleLiveConnection"/> is the in-memory one used by the smoke screenshots.
/// Events arrive on a background thread: dispatch to the UI thread before touching controls.
/// </summary>
public interface ILiveConnection : IAsyncDisposable
{
    event Action<CheckInResultDto>? CheckInRecorded;

    event Action<MemberChangedEvent>? MemberChanged;

    event Action<StockChangedEvent>? StockChanged;

    event Action<ProductChangedEvent>? ProductChanged;

    event Action<SaleCompletedEvent>? SaleCompleted;

    event Action<SettingsDto>? SettingsChanged;

    /// <summary>A new exchange rate was entered (or the rounding step changed).</summary>
    event Action<ExchangeRateStatusDto>? ExchangeRateChanged;

    /// <summary>Raised when <see cref="Status"/> changes.</summary>
    event Action<LiveStatus>? StatusChanged;

    LiveStatus Status { get; }

    /// <summary>Starts connecting and keeps trying in the background while the server cannot be reached. Returns at once.</summary>
    void Connect();

    Task StopAsync(CancellationToken cancellationToken = default);
}
