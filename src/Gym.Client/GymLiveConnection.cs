using Gym.Contracts;
using Microsoft.AspNetCore.Http.Connections.Client;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.Client;

/// <summary>
/// The SignalR connection to the server's live updates. Each app subscribes to the events it shows
/// (e.g. the dashboard to <see cref="CheckInRecorded"/>, the cashier screen to <see cref="StockChanged"/>).
/// It reconnects by itself after a network drop. Events arrive on a background thread: dispatch to the UI.
/// </summary>
public sealed class GymLiveConnection : IAsyncDisposable
{
    private readonly HubConnection connection;

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

    public HubConnectionState State => connection.State;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await connection.StartAsync(cancellationToken).ConfigureAwait(false);
        await RaiseStateChanged().ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken = default) => connection.StopAsync(cancellationToken);

    public ValueTask DisposeAsync() => connection.DisposeAsync();

    private Task RaiseStateChanged()
    {
        StateChanged?.Invoke(connection.State);
        return Task.CompletedTask;
    }
}
