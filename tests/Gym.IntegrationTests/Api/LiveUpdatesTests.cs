using Gym.Client;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace Gym.IntegrationTests.Api;

public sealed class LiveUpdatesTests(ApiFactory fx) : IClassFixture<ApiFactory>
{
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task Stations_get_check_ins_member_changes_and_stock_changes_live()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var admin = await fx.AdminAsync();
        var reception = await fx.ReceptionAsync();
        var cashier = await fx.CashierAsync();
        var product = await cashier.NewProductAsync(stock: 5);

        await using var live = new GymLiveConnection(fx.Server.BaseAddress, () => Task.FromResult(admin.AccessToken), options =>
        {
            options.HttpMessageHandlerFactory = _ => fx.Server.CreateHandler();
            options.Transports = HttpTransportType.LongPolling;
        });
        var checkIn = new TaskCompletionSource<CheckInResultDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var memberChanged = new TaskCompletionSource<MemberChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stockChanged = new TaskCompletionSource<StockChangedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var saleCompleted = new TaskCompletionSource<SaleCompletedEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        live.CheckInRecorded += e => checkIn.TrySetResult(e);
        live.MemberChanged += e => memberChanged.TrySetResult(e);
        live.StockChanged += e => stockChanged.TrySetResult(e);
        live.SaleCompleted += e => saleCompleted.TrySetResult(e);

        await live.StartAsync();
        Assert.Equal(HubConnectionState.Connected, live.State);

        var member = await reception.NewMemberAsync();
        Assert.Equal((member.Id, MemberChangeKind.Created), ((await memberChanged.Task.WaitAsync(Wait)).MemberId, (await memberChanged.Task).Kind));

        await reception.CheckInAsync(new CheckInRequest("Reception 2", Code: member.QrCode));
        var seen = await checkIn.Task.WaitAsync(Wait);
        Assert.Equal((member.Id, "Reception 2", CheckInResult.Active), (seen.MemberId, seen.Station, seen.Result));

        var receipt = await cashier.CreateSaleAsync(new CreateSaleRequest("Cafeteria 1", [new(product.Id, 2)]));
        Assert.Equal(receipt.Id, (await saleCompleted.Task.WaitAsync(Wait)).SaleId);
        var stock = await stockChanged.Task.WaitAsync(Wait);
        Assert.Equal(3, Assert.Single(stock.Products, p => p.ProductId == product.Id).Stock);
    }

    [Fact]
    public async Task Hub_needs_a_login()
    {
        await using var live = new GymLiveConnection(fx.Server.BaseAddress, () => Task.FromResult<string?>(null), options =>
        {
            options.HttpMessageHandlerFactory = _ => fx.Server.CreateHandler();
            options.Transports = HttpTransportType.LongPolling;
        });

        var error = await Assert.ThrowsAnyAsync<Exception>(() => live.StartAsync());
        Assert.Contains("401", error.ToString(), StringComparison.Ordinal);
    }
}
