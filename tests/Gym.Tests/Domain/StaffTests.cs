using Gym.Domain;

namespace Gym.Tests.Domain;

public sealed class StaffTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 9, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(StaffType.Admin, StaffRole.Admin)]
    [InlineData(StaffType.Reception, StaffRole.Reception)]
    [InlineData(StaffType.Cashier, StaffRole.Cashier)]
    [InlineData(StaffType.ClubTrainer, null)]
    public void Default_login_role_follows_the_job(StaffType type, StaffRole? role)
    {
        Assert.Equal(role, StaffMember.DefaultRoleFor(type));
    }

    [Fact]
    public void Login_is_optional()
    {
        var trainer = new StaffMember { Type = StaffType.ClubTrainer };
        Assert.False(trainer.HasLogin);
        Assert.False(trainer.CanLogIn);

        var cashier = new StaffMember { Type = StaffType.Cashier, Username = "sara", Role = StaffRole.Cashier, PasswordHash = "x" };
        Assert.True(cashier.CanLogIn);

        cashier.IsActive = false;
        Assert.False(cashier.CanLogIn);
    }

    [Theory]
    [InlineData(" Sara ", "sara")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Username_is_trimmed_and_lower_case(string? input, string? expected)
    {
        Assert.Equal(expected, StaffMember.NormalizeUsername(input));
    }

    [Theory]
    [InlineData("sara", true)]
    [InlineData("reception.1", true)]
    [InlineData("ab", false)]
    [InlineData("has space", false)]
    [InlineData("سارة", false)]
    public void Username_characters(string username, bool valid)
    {
        Assert.Equal(valid, StaffMember.IsValidUsername(username));
    }

    [Fact]
    public void Short_password_is_rejected()
    {
        Assert.Equal("invalid_password", Assert.Throws<DomainException>(() => StaffMember.EnsureValidPassword("12345")).Code);
        StaffMember.EnsureValidPassword("123456");
    }

    [Fact]
    public void Club_trainer_gets_a_monthly_rating_that_can_be_changed()
    {
        var trainer = new StaffMember { Id = 5, Type = StaffType.ClubTrainer };

        var rating = StaffRating.Create(trainer, 2026, 11, 4, "  Good classes ", ratedById: 1, Now);
        Assert.Equal((5, 2026, 11, 4, "Good classes"), (rating.StaffId, rating.Year, rating.Month, rating.Stars, rating.Note));

        rating.Update(5, "", ratedById: 2, Now.AddDays(1));
        Assert.Equal(5, rating.Stars);
        Assert.Null(rating.Note);
        Assert.Equal(2, rating.RatedById);
        Assert.Equal(Now, rating.CreatedAt);
        Assert.Equal(Now.AddDays(1), rating.UpdatedAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(6)]
    public void Rating_must_be_one_to_five_stars(int stars)
    {
        var trainer = new StaffMember { Type = StaffType.ClubTrainer };

        Assert.Equal("invalid_stars", Assert.Throws<DomainException>(() => StaffRating.Create(trainer, 2026, 11, stars, null, 1, Now)).Code);
    }

    [Theory]
    [InlineData(StaffType.Admin)]
    [InlineData(StaffType.Reception)]
    [InlineData(StaffType.Cashier)]
    public void Only_club_trainers_are_rated(StaffType type)
    {
        var staff = new StaffMember { Type = type };

        Assert.Equal("not_a_club_trainer", Assert.Throws<DomainException>(() => StaffRating.Create(staff, 2026, 11, 3, null, 1, Now)).Code);
    }

    [Theory]
    [InlineData(2026, 0)]
    [InlineData(2026, 13)]
    [InlineData(1999, 5)]
    public void Rating_month_must_be_valid(int year, int month)
    {
        var trainer = new StaffMember { Type = StaffType.ClubTrainer };

        Assert.Equal("invalid_month", Assert.Throws<DomainException>(() => StaffRating.Create(trainer, year, month, 3, null, 1, Now)).Code);
    }
}
