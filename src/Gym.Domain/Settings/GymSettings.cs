namespace Gym.Domain;

/// <summary>System-wide settings edited by the admin. One row.</summary>
public class GymSettings
{
    public const int SingletonId = 1;
    public const long DefaultPrivateTrainingMonthlyFee = 100_000;
    public const int MaxExpiringSoonDays = 60;

    public int Id { get; set; } = SingletonId;

    /// <summary>"Expiring soon" threshold in days (default 3).</summary>
    public int ExpiringSoonDays { get; set; } = SubscriptionRules.DefaultExpiringSoonDays;

    /// <summary>Fixed monthly fee of the private training service, whole SYP, the same for all private trainers.</summary>
    public long PrivateTrainingMonthlyFee { get; set; } = DefaultPrivateTrainingMonthlyFee;

    public DateTime UpdatedAt { get; set; }

    public void Update(int expiringSoonDays, long privateTrainingMonthlyFee, DateTime nowUtc)
    {
        if (expiringSoonDays is < 0 or > MaxExpiringSoonDays)
        {
            throw new DomainException("invalid_setting", $"The expiring-soon threshold must be 0 to {MaxExpiringSoonDays} days.");
        }

        if (privateTrainingMonthlyFee < 0)
        {
            throw new DomainException("invalid_setting", "The private training fee cannot be negative.");
        }

        ExpiringSoonDays = expiringSoonDays;
        PrivateTrainingMonthlyFee = privateTrainingMonthlyFee;
        UpdatedAt = nowUtc;
    }
}
