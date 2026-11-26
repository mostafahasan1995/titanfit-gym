using Microsoft.AspNetCore.Identity;

namespace Gym.IntegrationTests.Database;

/// <summary>A migrated database with the default data, plus helpers to build the data services directly.</summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private TestDatabase? database;

    public TestTimeProvider Time { get; } = new(TestTimeProvider.DefaultStart);

    public GymClock Clock => new(Time);

    public TestDatabase Database => database ?? throw new InvalidOperationException("Not initialized.");

    public async Task InitializeAsync()
    {
        database = TestDatabase.Create();
        await using var db = database.CreateContext();
        await new DatabaseInitializer(db).InitializeAsync();
    }

    public async Task DisposeAsync()
    {
        if (database is not null)
        {
            await database.DisposeAsync();
        }
    }

    public GymDbContext CreateContext() => Database.CreateContext();

    public SettingsService Settings(GymDbContext db) => new(db, Clock);

    public MemberService Members(GymDbContext db) => new(db, Clock, Settings(db));

    public StockService Stock(GymDbContext db) => new(db, Clock);

    public SalesService Sales(GymDbContext db) => new(db, Clock, Stock(db));

    public StaffService Staff(GymDbContext db) => new(db, Clock, new PasswordHasher<StaffMember>());

    public async Task<int> AddStaffAsync(StaffType type = StaffType.Reception)
    {
        await using var db = CreateContext();
        var staff = new StaffMember { FullName = $"{type} {Guid.NewGuid():N}"[..30], Type = type, CreatedAt = Clock.UtcNow };
        db.Staff.Add(staff);
        await db.SaveChangesAsync();
        return staff.Id;
    }

    /// <summary>A product in the first category with the given stock (one stock-in movement).</summary>
    public async Task<int> AddProductAsync(int stock, long price = 10_000, int minStock = 0)
    {
        await using var db = CreateContext();
        var staffId = await AddStaffAsync(StaffType.Cashier);
        var category = await db.ProductCategories.OrderBy(c => c.Id).FirstAsync();
        var product = new Product
        {
            NameEn = "Item " + Guid.NewGuid().ToString("N")[..8],
            NameAr = "صنف",
            CategoryId = category.Id,
            Price = price,
            MinStock = minStock,
            CreatedAt = Clock.UtcNow,
            UpdatedAt = Clock.UtcNow,
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        if (stock > 0)
        {
            db.StockMovements.Add(new StockMovement
            {
                ProductId = product.Id,
                Quantity = stock,
                Type = StockMovementType.StockIn,
                StaffId = staffId,
                At = Clock.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        return product.Id;
    }

    public async Task<int> StockOfAsync(int productId)
    {
        await using var db = CreateContext();
        return await db.StockMovements.Where(m => m.ProductId == productId).SumAsync(m => m.Quantity);
    }

    public static Member NewMember(string? nationalNo = null, string firstName = "Test", string lastName = "Member") => new()
    {
        Id = Guid.CreateVersion7(),
        FirstName = firstName,
        LastName = lastName,
        NationalNo = nationalNo,
        QrCode = MemberQrCode.Generate(),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };
}
