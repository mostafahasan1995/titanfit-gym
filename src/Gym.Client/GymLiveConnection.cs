using Gym.Contracts;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Client;

/// <summary>
/// The SignalR connection to the server's live updates. Each app subscribes to the events it shows
/// (e.g. the dashboard to <see cref="CheckInRecorded"/>, the cashier screen to <see cref="StockChanged"/>).
/// It reconnects by itself after a network drop; <see cref="Connect"/> also keeps retrying while the server is down.
/// Events arrive on a background thread: dispatch to the UI.
/// </summary>
public sealed class GymLiveConnection : ILiveConnection
{
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private readonly HubConnection connection;
    private CancellationTokenSource? keepConnected;
    private LiveStatus status = LiveStatus.Offline;

    /// <param name="serverAddress">The server, e.g. <c>http://admin-pc:5080/</c>.</param>
    /// <param name="accessTokenProvider">Returns the current login token (e.g. <c>() =&gt; Task.FromResult(api.AccessToken)</c>).</param>
    /// <param name="configure">Optional transport settings (tests use this to reach an in-memory server).</param>
    public GymLiveConnection(Uri serverAddress, Func<Task<string?>> accessTokenProvider, Action<HttpConnectionOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(serverAddress);
        connection = new HubConnectionBuilder()
            .WithUrl(new Uri(serverAddress, GymHubRoute.Path.TrimStart('/')), options =>
            {
                options.AccessTokenProvider = accessTokenProvider;
                configure?.Invoke(options);
            })
            .WithAutomaticReconnect()
            .AddJsonProtocol(options => GymJson.Configure(options.PayloadSerializerOptions))
            .Build();

        connection.On<CheckInResultDto>(nameof(IGymHubClient.CheckInRecorded), e => CheckInRecorded?.Invoke(e));
        connection.On<MemberChangedEvent>(nameof(IGymHubClient.MemberChanged), e => MemberChanged?.Invoke(e));
        connection.On<StockChangedEvent>(nameof(IGymHubClient.StockChanged), e => StockChanged?.Invoke(e));
        connection.On<ProductChangedEvent>(nameof(IGymHubClient.ProductChanged), e => ProductChanged?.Invoke(e));
        connection.On<SaleCompletedEvent>(nameof(IGymHubClient.SaleCompleted), e => SaleCompleted?.Invoke(e));
        connection.On<SettingsDto>(nameof(IGymHubClient.SettingsChanged), e => SettingsChanged?.Invoke(e));
        connection.Reconnecting += _ => RaiseStateChanged();
        connection.Reconnected += _ => RaiseStateChanged();
        connection.Closed += _ => RaiseStateChanged();
    }

    public event Action<CheckInResultDto>? CheckInRecorded;

    public event Action<MemberChangedEvent>? MemberChanged;

    public event Action<StockChangedEvent>? StockChanged;

    public event Action<ProductChangedEvent>? ProductChanged;

    public event Action<SaleCompletedEvent>? SaleCompleted;

    public event Action<SettingsDto>? SettingsChanged;

    /// <summary>Connected, reconnecting or closed (for a status light in the apps).</summary>
    public event Action<HubConnectionState>? StateChanged;

    /// <inheritdoc />
    public event Action<LiveStatus>? StatusChanged;

    public HubConnectionState State => connection.State;

    /// <inheritdoc />
    public LiveStatus Status => status;

    /// <summary>Connects once; throws when the server refuses or cannot be reached. See also <see cref="Connect"/>.</summary>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await connection.StartAsync(cancellationToken).ConfigureAwait(false);
        await RaiseStateChanged().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public void Connect()
    {
        if (keepConnected is not null)
        {
            return;
        }

        keepConnected = new CancellationTokenSource();
        _ = KeepConnectedAsync(keepConnected.Token);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        CancelKeepConnected();
        await connection.StopAsync(cancellationToken).ConfigureAwait(false);
        await RaiseStateChanged().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        CancelKeepConnected();
        await connection.DisposeAsync().ConfigureAwait(false);
    }

    /// <summary>Starts the connection whenever it is down (first start, or closed after the automatic reconnects gave up).</summary>
    private async Task KeepConnectedAsync(CancellationToken ct)
    {
        var firstAttempt = true;
        while (!ct.IsCancellationRequested)
        {
            if (connection.State == HubConnectionState.Disconnected)
            {
                if (firstAttempt)
                {
                    SetStatus(LiveStatus.Connecting);
                    firstAttempt = false;
                }

                try
                {
                    await connection.StartAsync(ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    break;
                }
#pragma warning disable CA1031 // Any failure (server down, refused, 401) only means: try again after the delay.
                catch (Exception)
#pragma warning restore CA1031
                {
                }

                await RaiseStateChanged().ConfigureAwait(false);
            }

            try
            {
                await Task.Delay(RetryDelay, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private void CancelKeepConnected()
    {
        var cts = Interlocked.Exchange(ref keepConnected, null);
        if (cts is not null)
        {
            cts.Cancel();
            cts.Dispose();
        }
    }

    private Task RaiseStateChanged()
    {
        var state = connection.State;
        StateChanged?.Invoke(state);
        SetStatus(state switch
        {
            HubConnectionState.Connected => LiveStatus.Connected,
            HubConnectionState.Connecting => LiveStatus.Connecting,
            HubConnectionState.Reconnecting => LiveStatus.Reconnecting,
            _ => LiveStatus.Offline,
        });
        return Task.CompletedTask;
    }

    private void SetStatus(LiveStatus value)
    {
        if (status == value)
        {
            return;
        }

        status = value;
        StatusChanged?.Invoke(value);
    }
}
