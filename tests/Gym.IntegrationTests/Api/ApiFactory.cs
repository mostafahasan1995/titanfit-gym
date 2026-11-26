using Gym.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Gym.IntegrationTests.Api;

/// <summary>
/// The real API in memory on its own PostgreSQL database (migrated at start like on the server), with a
/// settable business clock. Test-only passwords below; nothing here is used outside the tests.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string AdminUsername = "owner";
    public const string AdminPassword = "owner-test-only";
    public const string StaffPassword = "staff-test-only";

    private readonly TestDatabase database = TestDatabase.Create();
    private readonly string dataPath = Path.Combine(Path.GetTempPath(), "titanfit-it-" + Guid.NewGuid().ToString("N"));
    private readonly SemaphoreSlim adminGate = new(1, 1);
    private string? adminToken;

    public TestTimeProvider Time { get; } = new(TestTimeProvider.DefaultStart);

    public DateOnly Today => Time.Today;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Gym"] = database.ConnectionString,
            ["Database:MigrateOnStartup"] = "true",
            ["Storage:DataPath"] = dataPath,
        }));
        builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<GymClock>();
            services.AddSingleton(new GymClock(Time));
        });
    }

    public Task InitializeAsync()
    {
        _ = Server; // starts the host; the migrations run before it listens
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await database.DisposeAsync();
        adminGate.Dispose();
        if (Directory.Exists(dataPath))
        {
            Directory.Delete(dataPath, recursive: true);
        }
    }

    public GymApiClient Anonymous() => new(CreateClient());

    public GymApiClient WithToken(string token) => new(CreateClient()) { AccessToken = token };

    /// <summary>The first admin (created through the first-run setup on first use).</summary>
    public async Task<GymApiClient> AdminAsync()
    {
        string token;
        await adminGate.WaitAsync();
        try
        {
            token = adminToken ??= (await Anonymous().SetupAdminAsync(new SetupAdminRequest("Owner", AdminUsername, AdminPassword))).Token;
        }
        finally
        {
            adminGate.Release();
        }

        return WithToken(token);
    }

    /// <summary>A new staff member with a login, logged in.</summary>
    public async Task<(GymApiClient Client, StaffDto Staff)> StaffAsync(StaffRole role)
    {
        var admin = await AdminAsync();
        var username = role.ToString().ToLowerInvariant() + Guid.NewGuid().ToString("N")[..8];
        var type = role switch
        {
            StaffRole.Admin => StaffType.Admin,
            StaffRole.Reception => StaffType.Reception,
            _ => StaffType.Cashier,
        };
        var staff = await admin.CreateStaffAsync(new CreateStaffRequest($"{role} {username}", type, Username: username, Password: StaffPassword, Role: role));
        var client = Anonymous();
        await client.LoginAsync(username, StaffPassword);
        return (client, staff);
    }

    public async Task<GymApiClient> ReceptionAsync() => (await StaffAsync(StaffRole.Reception)).Client;

    public async Task<GymApiClient> CashierAsync() => (await StaffAsync(StaffRole.Cashier)).Client;
}

internal static class TestData
{
    public static readonly byte[] TinyJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0xFF, 0xD9];

    public static async Task<int> PlanIdAsync(this GymApiClient client, int months) =>
        (await client.GetPlansAsync()).First(p => p.Months == months).Id;

    public static string NationalNo() => "0" + Random.Shared.NextInt64(1_000_000_000, 9_999_999_999);

    public static async Task<MemberDto> NewMemberAsync(
        this GymApiClient client, int? planMonths = 1, MemberType type = MemberType.Regular, string firstName = "Sami", string lastName = "Haddad")
    {
        int? planId = planMonths is { } months ? await client.PlanIdAsync(months) : null;
        return await client.CreateMemberAsync(new CreateMemberRequest(
            firstName, lastName, FatherName: "Omar", NationalNo: NationalNo(), Phone: "09" + Random.Shared.Next(10_000_000, 99_999_999),
            Type: type, PlanId: planId));
    }

    public static async Task<ProductDto> NewProductAsync(this GymApiClient client, long price = 10_000, int stock = 0, int minStock = 0)
    {
        var categoryId = (await client.GetCategoriesAsync()).First().Id;
        var product = await client.CreateProductAsync(new SaveProductRequest(
            "Item " + Guid.NewGuid().ToString("N")[..6], "صنف", categoryId, price, Barcode: "62" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999), MinStock: minStock));
        if (stock > 0)
        {
            await client.StockInAsync(new StockInRequest(product.Id, stock));
        }

        return product;
    }
}
