using Microsoft.EntityFrameworkCore.Design;

namespace Gym.Infrastructure.Persistence;

/// <summary>Used by <c>dotnet ef migrations add</c>; it never connects to a database.</summary>
public sealed class DesignTimeGymDbContextFactory : IDesignTimeDbContextFactory<GymDbContext>
{
    public GymDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<GymDbContext>();
        GymDbContextOptions.Configure(builder, "Host=localhost;Database=titanfit_design;Username=postgres");
        return new GymDbContext(builder.Options);
    }
}
