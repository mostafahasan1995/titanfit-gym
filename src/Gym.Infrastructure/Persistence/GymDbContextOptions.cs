namespace Gym.Infrastructure.Persistence;

public static class GymDbContextOptions
{
    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>Used only when no connection string is configured, so the API still starts (and logs an error).</summary>
    public const string PlaceholderConnectionString = "Host=localhost;Database=titanfit;Username=titanfit";

    public static void Configure(DbContextOptionsBuilder options, string? connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);
        options
            .UseNpgsql(
                string.IsNullOrWhiteSpace(connectionString) ? PlaceholderConnectionString : connectionString,
                npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable))
            .UseSnakeCaseNamingConvention();
    }
}
