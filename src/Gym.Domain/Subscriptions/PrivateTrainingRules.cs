namespace Gym.Domain;

/// <summary>
/// The private training service a private trainer pays to train clients at the gym.
/// Price = months × one fixed monthly fee from the settings, the same for all private trainers.
/// Its dates and status use the same rules as a membership.
/// </summary>
public static class PrivateTrainingRules
{
    public static decimal CalculatePrice(int months, decimal monthlyFee)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(months, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(monthlyFee);
        return checked(months * monthlyFee);
    }

    public static void EnsureCanHaveService(Member member)
    {
        if (member.Type != MemberType.PrivateTrainer)
        {
            throw new DomainException(
                "not_a_private_trainer",
                $"Member {MemberNumber.Format(member.MemberNo)} is not a private trainer.");
        }
    }
}
