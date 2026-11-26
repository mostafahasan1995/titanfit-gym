namespace Gym.Domain;

public enum SubscriptionStatus
{
    /// <summary>More days left than the expiring-soon threshold.</summary>
    Active = 0,

    /// <summary>0 to threshold days left. 0 means "expires today" (the end date is still valid).</summary>
    ExpiringSoon = 1,

    /// <summary>Today is after the end date.</summary>
    Expired = 2,
}

/// <summary>Result of <see cref="SubscriptionRules.GetStatus"/>.</summary>
/// <param name="Status">Active, ExpiringSoon or Expired.</param>
/// <param name="DaysLeft"><c>EndDate - today</c> while valid (0 on the end date), otherwise 0.</param>
/// <param name="DaysOverdue"><c>today - EndDate</c> when expired, otherwise 0.</param>
public readonly record struct SubscriptionState(SubscriptionStatus Status, int DaysLeft, int DaysOverdue)
{
    /// <summary>True on and before the end date.</summary>
    public bool IsValid => Status != SubscriptionStatus.Expired;
}
