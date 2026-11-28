using Gym.Contracts;

namespace Gym.Client.Sample;

/// <summary>
/// Live updates without a server: always "connected". <see cref="SampleGymApi"/> publishes the events of its own
/// changes here (check-ins, member changes, sales, stock), so screens react like they would with the real server.
/// </summary>
public sealed class SampleLiveConnection : ILiveConnection
{
    public event Action<CheckInResultDto>? CheckInRecorded;

    public event Action<MemberChangedEvent>? MemberChanged;

    public event Action<StockChangedEvent>? StockChanged;

    public event Action<ProductChangedEvent>? ProductChanged;

    public event Action<SaleCompletedEvent>? SaleCompleted;

    public event Action<SettingsDto>? SettingsChanged;

    public event Action<ExchangeRateStatusDto>? ExchangeRateChanged;

    public event Action<LiveStatus>? StatusChanged;

    public LiveStatus Status { get; private set; } = LiveStatus.Offline;

    public void Connect() => SetStatus(LiveStatus.Connected);

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        SetStatus(LiveStatus.Offline);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    /// <summary>Shows a status in the header (for trying the reconnecting / offline looks).</summary>
    public void SetStatus(LiveStatus status)
    {
        if (Status != status)
        {
            Status = status;
            StatusChanged?.Invoke(status);
        }
    }

    public void PublishCheckIn(CheckInResultDto checkIn) => CheckInRecorded?.Invoke(checkIn);

    public void PublishMemberChanged(MemberChangedEvent change) => MemberChanged?.Invoke(change);

    public void PublishStockChanged(StockChangedEvent change) => StockChanged?.Invoke(change);

    public void PublishProductChanged(ProductChangedEvent change) => ProductChanged?.Invoke(change);

    public void PublishSaleCompleted(SaleCompletedEvent sale) => SaleCompleted?.Invoke(sale);

    public void PublishSettingsChanged(SettingsDto settings) => SettingsChanged?.Invoke(settings);

    public void PublishExchangeRateChanged(ExchangeRateStatusDto status) => ExchangeRateChanged?.Invoke(status);
}
