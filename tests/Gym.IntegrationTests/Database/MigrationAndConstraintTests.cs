namespace Gym.IntegrationTests.Database;

public sealed class MigrationAndConstraintTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Migration_applies_and_matches_the_model()
    {
        await using var db = fx.CreateContext();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Contains(await db.Database.GetAppliedMigrationsAsync(), m => m.EndsWith("_InitialCreate", StringComparison.Ordinal));
        Assert.False(db.Database.HasPendingModelChanges(), "The model has changes that are not in a migration.");
    }

    [Fact]
    public async Task Defaults_are_seeded_once()
    {
        await using (var again = fx.CreateContext())
        {
            await new DatabaseInitializer(again).InitializeAsync();
        }

        await using var db = fx.CreateContext();
        var settings = await db.Settings.SingleAsync();
        Assert.Equal(3, settings.ExpiringSoonDays);
        Assert.Equal(25m, settings.PrivateTrainingMonthlyFee);
        Assert.Equal(1, settings.SypRoundingStep);
        Assert.Equal(new[] { 1, 3, 6, 12 }, await db.Plans.OrderBy(p => p.Months).Select(p => p.Months).ToListAsync());
        Assert.Equal(
            new[] { "Drinks", "Shakes", "Snacks", "Supplements" },
            await db.ProductCategories.OrderBy(c => c.SortOrder).Select(c => c.NameEn).ToListAsync());
    }

    [Fact]
    public async Task National_number_is_unique_but_optional()
    {
        var national = "0" + Random.Shared.NextInt64(1_000_000_000, 9_999_999_999);
        await AddAsync(PostgresFixture.NewMember(national));

        await AssertUniqueViolationAsync("ux_members_national_no", PostgresFixture.NewMember(national));

        // Many members without a national number are fine.
        await AddAsync(PostgresFixture.NewMember());
        await AddAsync(PostgresFixture.NewMember());
    }

    [Fact]
    public async Task Qr_code_is_unique()
    {
        var first = PostgresFixture.NewMember();
        await AddAsync(first);
        var second = PostgresFixture.NewMember();
        second.QrCode = first.QrCode;

        await AssertUniqueViolationAsync("ux_members_qr_code", second);
    }

    [Fact]
    public async Task Member_number_is_unique()
    {
        var first = PostgresFixture.NewMember();
        await AddAsync(first);
        var second = PostgresFixture.NewMember();
        second.MemberNo = first.MemberNo;

        await AssertUniqueViolationAsync("ux_members_member_no", second);
    }

    [Fact]
    public async Task Product_barcode_is_unique()
    {
        int categoryId;
        await using (var db = fx.CreateContext())
        {
            categoryId = (await db.ProductCategories.FirstAsync()).Id;
        }

        var barcode = "62" + Random.Shared.NextInt64(10_000_000_000, 99_999_999_999);
        Product NewProduct() => new() { NameEn = "Water", NameAr = "ماء", CategoryId = categoryId, Barcode = barcode, Price = 0.50m };
        await AddAsync(NewProduct());

        await AssertUniqueViolationAsync("ux_products_barcode", NewProduct());
    }

    [Fact]
    public async Task Username_is_unique()
    {
        var username = "user" + Random.Shared.Next(100_000, 999_999);
        await AddAsync(new StaffMember { FullName = "A", Type = StaffType.Cashier, Username = username, Role = StaffRole.Cashier });

        await AssertUniqueViolationAsync("ux_staff_username", new StaffMember { FullName = "B", Type = StaffType.Cashier, Username = username, Role = StaffRole.Cashier });
    }

    [Fact]
    public async Task Duplicates_become_conflicts_with_a_stable_code()
    {
        var national = "1" + Random.Shared.NextInt64(1_000_000_000, 9_999_999_999);
        await AddAsync(PostgresFixture.NewMember(national));

        await using var db = fx.CreateContext();
        db.Members.Add(PostgresFixture.NewMember(national));
        var conflict = await Assert.ThrowsAsync<ConflictException>(() => db.SaveChangesOrConflictAsync());

        Assert.Equal("duplicate_national_no", conflict.Code);
    }

    [Fact]
    public async Task Member_numbers_come_from_a_sequence_even_when_registering_at_the_same_time()
    {
        var numbers = await Task.WhenAll(Enumerable.Range(0, 20).Select(async _ =>
        {
            await using var db = fx.CreateContext();
            var member = PostgresFixture.NewMember();
            db.Members.Add(member);
            await db.SaveChangesAsync();
            return member.MemberNo;
        }));

        Assert.Equal(20, numbers.Distinct().Count());
        Assert.All(numbers, n => Assert.True(n > 0));
    }

    [Fact]
    public async Task One_open_shift_per_cashier()
    {
        var cashierId = await fx.AddStaffAsync(StaffType.Cashier);
        await AddAsync(new Shift { CashierId = cashierId, Station = "Cafeteria 1", OpenedAt = DateTime.UtcNow });

        await AssertUniqueViolationAsync("ux_shifts_open_per_cashier", new Shift { CashierId = cashierId, Station = "Cafeteria 2", OpenedAt = DateTime.UtcNow });

        // A closed shift does not count.
        await AddAsync(new Shift { CashierId = cashierId, Station = "Cafeteria 2", OpenedAt = DateTime.UtcNow, ClosedAt = DateTime.UtcNow });
    }

    [Fact]
    public async Task Check_constraints_guard_the_data()
    {
        var member = PostgresFixture.NewMember();
        await AddAsync(member);
        var staffId = await fx.AddStaffAsync();

        await using var db = fx.CreateContext();
        db.Subscriptions.Add(new Subscription
        {
            MemberId = member.Id,
            Months = 1,
            StartDate = new DateOnly(2026, 11, 26),
            EndDate = new DateOnly(2026, 11, 26), // end must be after start
            CreatedById = staffId,
            CreatedAt = DateTime.UtcNow,
        });

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Contains("ck_subscriptions_dates", error.InnerException?.Message ?? "", StringComparison.Ordinal);
    }

    private async Task AddAsync(object entity)
    {
        await using var db = fx.CreateContext();
        db.Add(entity);
        await db.SaveChangesAsync();
    }

    private async Task AssertUniqueViolationAsync(string expectedConstraint, object entity)
    {
        await using var db = fx.CreateContext();
        db.Add(entity);
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.True(DbErrors.IsUniqueViolation(error, out var constraint), error.InnerException?.Message);
        Assert.Equal(expectedConstraint, constraint);
    }
}
