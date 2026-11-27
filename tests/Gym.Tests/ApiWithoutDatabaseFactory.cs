using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Gym.Tests;

/// <summary>Hosts the API in memory (Development) without a database: no migration at start.</summary>
public sealed class ApiWithoutDatabaseFactory : WebApplicationFactory<Program>
{
    private readonly string dataPath = Path.Combine(Path.GetTempPath(), "titanfit-tests-" + Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Database:MigrateOnStartup"] = "false",
            ["Storage:DataPath"] = dataPath,
            ["Backup:ScheduleEnabled"] = "false",
        }));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(dataPath))
        {
            Directory.Delete(dataPath, recursive: true);
        }
    }
}
