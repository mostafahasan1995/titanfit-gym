using Microsoft.AspNetCore.SignalR;

namespace Gym.Api.Live;

/// <summary>Sends events to all stations. A failed broadcast is logged and never fails the request.</summary>
public sealed class LiveUpdates(IHubContext<GymHub, IGymHubClient> hub, ILogger<LiveUpdates> logger)
{
    public Task CheckInAsync(CheckInResultDto checkIn) => SendAsync(clients => clients.CheckInRecorded(checkIn));

    public Task MemberChangedAsync(Guid memberId, MemberChangeKind kind) =>
        SendAsync(clients => clients.MemberChanged(new MemberChangedEvent(memberId, kind)));

    public Task StockChangedAsync(IReadOnlyList<StockLevelDto> levels) =>
        levels.Count == 0 ? Task.CompletedTask : SendAsync(clients => clients.StockChanged(new StockChangedEvent(levels)));

    public Task ProductChangedAsync(int productId) => SendAsync(clients => clients.ProductChanged(new ProductChangedEvent(productId)));

    public Task SaleCompletedAsync(ReceiptDto receipt) =>
        SendAsync(clients => clients.SaleCompleted(new SaleCompletedEvent(receipt.Id, receipt.Number, receipt.Station, receipt.Total, receipt.At)));

    public Task SettingsChangedAsync(SettingsDto settings) => SendAsync(clients => clients.SettingsChanged(settings));

    private async Task SendAsync(Func<IGymHubClient, Task> send)
    {
        try
        {
            await send(hub.Clients.All);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "A live update could not be sent.");
        }
    }
}
