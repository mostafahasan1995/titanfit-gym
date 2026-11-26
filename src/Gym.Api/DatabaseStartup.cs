using Gym.Infrastructure.Persistence;

namespace Gym.Api;

internal static class DatabaseStartup
{
    /// <summary>Applies the migrations and the default data before the server starts listening.</summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue("Database:MigrateOnStartup", true))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(app.Configuration.GetConnectionString("Gym")))
        {
            app.Logger.LogError(
                "No database connection string. Set ConnectionStrings:Gym in appsettings.json or the ConnectionStrings__Gym environment variable.");
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
    }
}
