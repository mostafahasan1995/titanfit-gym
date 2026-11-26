using Npgsql;

namespace Gym.IntegrationTests;

/// <summary>
/// A new, empty PostgreSQL database for one test class. The server comes from the GYM_TEST_POSTGRES environment
/// variable (a connection string without a database). The migrations create the database; it is dropped at the end.
/// </summary>
public sealed class TestDatabase : IAsyncDisposable
{
    public const string EnvironmentVariable = "GYM_TEST_POSTGRES";

    private readonly string serverConnectionString;

    private TestDatabase(string serverConnectionString, string name)
    {
        this.serverConnectionString = serverConnectionString;
        Name = name;
        ConnectionString = new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = name }.ConnectionString;
    }

    public string Name { get; }

    public string ConnectionString { get; }

    public static TestDatabase Create()
    {
        var server = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(server))
        {
            throw new InvalidOperationException(
                $"Set {EnvironmentVariable} to a PostgreSQL connection string without a database, " +
                "e.g. Host=localhost;Port=5432;Username=postgres;Password=postgres");
        }

        return new TestDatabase(server, "gym_it_" + Guid.NewGuid().ToString("N")[..12]);
    }

    public GymDbContext CreateContext()
    {
        var builder = new DbContextOptionsBuilder<GymDbContext>();
        GymDbContextOptions.Configure(builder, ConnectionString);
        return new GymDbContext(builder.Options);
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        var admin = new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = "postgres", Pooling = false };
        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{Name}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }
}
