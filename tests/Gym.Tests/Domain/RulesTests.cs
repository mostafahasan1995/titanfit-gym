using Gym.Domain;

namespace Gym.Tests.Domain;

public sealed class RulesTests
{
    [Theory]
    [InlineData(1, 25, 25)]
    [InlineData(3, 25, 75)]
    [InlineData(12, 12.5, 150)]
    [InlineData(2, 0, 0)]
    public void Private_training_price_is_months_times_the_fixed_fee(int months, double fee, double expected)
    {
        Assert.Equal((decimal)expected, PrivateTrainingRules.CalculatePrice(months, (decimal)fee));
    }

    [Fact]
    public void Private_training_price_rejects_bad_input()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PrivateTrainingRules.CalculatePrice(0, 25m));
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
        Assert.Equal((3, 25m, 1), (settings.ExpiringSoonDays, settings.PrivateTrainingMonthlyFee, settings.SypRoundingStep));

        settings.Update(5, 30.5m, DateTime.UtcNow, sypRoundingStep: 500);
        Assert.Equal((5, 30.5m, 500), (settings.ExpiringSoonDays, settings.PrivateTrainingMonthlyFee, settings.SypRoundingStep));

        Assert.Equal("invalid_setting", Assert.Throws<DomainException>(() => settings.Update(-1, 1, DateTime.UtcNow)).Code);
        Assert.Equal("invalid_setting", Assert.Throws<DomainException>(() => settings.Update(3, -1, DateTime.UtcNow)).Code);
        Assert.Equal("invalid_setting", Assert.Throws<DomainException>(() => settings.Update(3, 1, DateTime.UtcNow, sypRoundingStep: 0)).Code);
    }

    [Fact]
    public void Default_plans_match_the_sample_prices()
    {
        Assert.Equal(
            new[] { (1, 20m), (3, 55m), (6, 100m), (12, 180m) },
            Plan.Defaults().Select(p => (p.Months, p.Price)));
    }
}
