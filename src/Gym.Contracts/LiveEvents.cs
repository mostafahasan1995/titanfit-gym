namespace Gym.Contracts;

/// <summary>
/// Events the server pushes to every logged-in station over SignalR (<see cref="GymHubRoute.Path"/>). The method
/// names are the event names.
/// </summary>
public interface IGymHubClient
{
    Task CheckInRecorded(CheckInResultDto checkIn);

    Task MemberChanged(MemberChangedEvent change);

    Task StockChanged(StockChangedEvent change);

    Task ProductChanged(ProductChangedEvent change);

    Task SaleCompleted(SaleCompletedEvent sale);

    Task SettingsChanged(SettingsDto settings);

    /// <summary>The manager entered a new exchange rate (or the rounding step changed).</summary>
    Task ExchangeRateChanged(ExchangeRateStatusDto status);
}

public static class GymHubRoute
{
    public const string Path = "/hubs/gym";
}

public enum MemberChangeKind
{
    Created,
    Updated,
    Renewed,
    QrCodeReissued,
    PhotoChanged,
    TrainerLinkChanged,
    PrivateServiceChanged,
}

public sealed record MemberChangedEvent(Guid MemberId, MemberChangeKind Kind);

public sealed record StockChangedEvent(IReadOnlyList<StockLevelDto> Products);

public sealed record ProductChangedEvent(int ProductId);

public sealed record SaleCompletedEvent(long SaleId, long Number, string Station, decimal Total, DateTime At);
