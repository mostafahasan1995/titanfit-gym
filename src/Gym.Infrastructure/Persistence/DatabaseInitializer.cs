namespace Gym.Infrastructure.Persistence;

/// <summary>Applies the migrations (creating the database if needed) and adds the default plans and categories once.</summary>
public sealed class DatabaseInitializer(GymDbContext db)
{
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);
        await SeedDefaultsAsync(ct);
    }

    public async Task SeedDefaultsAsync(CancellationToken ct = default)
    {
        if (!await db.Plans.AnyAsync(ct))
        {
            db.Plans.AddRange(Plan.Defaults());
        }

        if (!await db.ProductCategories.AnyAsync(ct))
        {
            db.ProductCategories.AddRange(ProductCategory.Defaults());
        }

        await db.SaveChangesAsync(ct);
    }
}
