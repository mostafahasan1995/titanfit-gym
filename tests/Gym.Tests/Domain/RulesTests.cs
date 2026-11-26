using Gym.Domain;

namespace Gym.Tests.Domain;

public sealed class RulesTests
{
    [Theory]
    [InlineData(1, 100_000, 100_000)]
    [InlineData(3, 100_000, 300_000)]
    [InlineData(12, 125_000, 1_500_000)]
    [InlineData(2, 0, 0)]
    public void Private_training_price_is_months_times_the_fixed_fee(int months, long fee, long expected)
    {
        Assert.Equal(expected, PrivateTrainingRules.CalculatePrice(months, fee));
    }

    [Fact]
    public void Private_training_price_rejects_bad_input()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PrivateTrainingRules.CalculatePrice(0, 100_000));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrivateTrainingRules.CalculatePrice(1, -1));
    }

    [Fact]
    public void Only_private_trainers_can_have_the_private_training_service()
    {
        PrivateTrainingRules.EnsureCanHaveService(new Member { Type = MemberType.PrivateTrainer });

        Assert.Equal(
            "not_a_private_trainer",
            Assert.Throws<DomainException>(() => PrivateTrainingRules.EnsureCanHaveService(new Member { Type = MemberType.Regular })).Code);
    }

    [Theory]
    [InlineData(SubscriptionStatus.Active, CheckInResult.Active, true)]
    [InlineData(SubscriptionStatus.ExpiringSoon, CheckInResult.ExpiringSoon, true)]
    [InlineData(SubscriptionStatus.Expired, CheckInResult.Expired, false)]
    public void Check_in_result_follows_the_membership_status(SubscriptionStatus status, CheckInResult result, bool admitted)
    {
        var actual = CheckInRules.ResultFor(new SubscriptionState(status, 0, 0));

        Assert.Equal(result, actual);
        Assert.Equal(admitted, CheckInRules.IsAdmitted(actual));
    }

    [Fact]
    public void Check_in_without_any_membership()
    {
        Assert.Equal(CheckInResult.NoSubscription, CheckInRules.ResultFor(null));
        Assert.False(CheckInRules.IsAdmitted(CheckInResult.NoSubscription));
        Assert.False(CheckInRules.IsAdmitted(CheckInResult.UnknownMember));
    }

    [Fact]
    public void Settings_defaults_and_validation()
    {
        var settings = new GymSettings();
        Assert.Equal((3, 100_000L), (settings.ExpiringSoonDays, settings.PrivateTrainingMonthlyFee));

        settings.Update(5, 120_000, DateTime.UtcNow);
        Assert.Equal((5, 120_000L), (settings.ExpiringSoonDays, settings.PrivateTrainingMonthlyFee));

        Assert.Equal("invalid_setting", Assert.Throws<DomainException>(() => settings.Update(-1, 1, DateTime.UtcNow)).Code);
        Assert.Equal("invalid_setting", Assert.Throws<DomainException>(() => settings.Update(3, -1, DateTime.UtcNow)).Code);
    }

    [Fact]
    public void Default_plans_match_the_sample_prices()
    {
        Assert.Equal(
            new[] { (1, 150_000L), (3, 400_000L), (6, 750_000L), (12, 1_400_000L) },
            Plan.Defaults().Select(p => (p.Months, p.Price)));
    }
}
