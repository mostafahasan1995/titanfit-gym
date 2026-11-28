namespace Gym.Domain;

/// <summary>System-wide settings edited by the admin. One row.</summary>
public class GymSettings
{
    public const int SingletonId = 1;
    public const decimal DefaultPrivateTrainingMonthlyFee = 25m;
    public const int MaxExpiringSoonDays = 60;

    public int Id { get; set; } = SingletonId;

    /// <summary>"Expiring soon" threshold in days (default 3).</summary>
    public int ExpiringSoonDays { get; set; } = SubscriptionRules.DefaultExpiringSoonDays;

    /// <summary>Fixed monthly fee of the private training service in US dollars, the same for all private trainers.</summary>
    public decimal PrivateTrainingMonthlyFee { get; set; } = DefaultPrivateTrainingMonthlyFee;

    /// <summary>Amounts in Syrian pounds are rounded to a multiple of this many pounds (default 1).</summary>
    public int SypRoundingStep { get; set; } = CurrencyRules.DefaultSypRoundingStep;

    public DateTime UpdatedAt { get; set; }

    public void Update(int expiringSoonDays, decimal privateTrainingMonthlyFee, DateTime nowUtc, int sypRoundingStep = CurrencyRules.DefaultSypRoundingStep)
    {
        if (expiringSoonDays is < 0 or > MaxExpiringSoonDays)
        {
            throw new DomainException("invalid_setting", $"The expiring-soon threshold must be 0 to {MaxExpiringSoonDays} days.");
        }

        if (privateTrainingMonthlyFee < 0 || privateTrainingMonthlyFee > CurrencyRules.MaxUsd)
        {
            throw new DomainException("invalid_setting", "The private training fee cannot be negative.");
        }

        if (sypRoundingStep is < 1 or > CurrencyRules.MaxSypRoundingStep)
        {
            throw new DomainException("invalid_setting", $"The rounding step must be 1 to {CurrencyRules.MaxSypRoundingStep} pounds.");
        }

        ExpiringSoonDays = expiringSoonDays;
        PrivateTrainingMonthlyFee = CurrencyRules.RoundUsd(privateTrainingMonthlyFee);
        SypRoundingStep = sypRoundingStep;
        UpdatedAt = nowUtc;
    }
}
