namespace Gym.Infrastructure.Persistence;

/// <summary>The PostgreSQL database. Table and column names are snake_case.</summary>
public sealed class GymDbContext(DbContextOptions<GymDbContext> options) : DbContext(options)
{
    /// <summary>Gives <see cref="Member.MemberNo"/>; safe when 2 reception PCs register at the same time.</summary>
    public const string MemberNumberSequence = "member_numbers";

    /// <summary>Gives <see cref="Sale.Number"/> (the receipt number).</summary>
    public const string SaleNumberSequence = "sale_numbers";

    public DbSet<Member> Members => Set<Member>();

    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    public DbSet<Plan> Plans => Set<Plan>();

    public DbSet<CheckIn> CheckIns => Set<CheckIn>();

    public DbSet<StaffMember> Staff => Set<StaffMember>();

    public DbSet<StaffRating> StaffRatings => Set<StaffRating>();

    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();

    public DbSet<Product> Products => Set<Product>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<Sale> Sales => Set<Sale>();

    public DbSet<SaleLine> SaleLines => Set<SaleLine>();

    public DbSet<Shift> Shifts => Set<Shift>();

    public DbSet<GymSettings> Settings => Set<GymSettings>();

    public DbSet<ExchangeRate> ExchangeRates => Set<ExchangeRate>();

    /// <summary>Money columns: US dollars with cents, pounds and rates with 2 decimals (numeric(14,2)).</summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        ArgumentNullException.ThrowIfNull(configurationBuilder);
        configurationBuilder.Properties<decimal>().HavePrecision(14, 2);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<int>(MemberNumberSequence).StartsAt(1).IncrementsBy(1);
        modelBuilder.HasSequence<long>(SaleNumberSequence).StartsAt(1).IncrementsBy(1);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GymDbContext).Assembly);
    }
}
