namespace Gym.Domain;

/// <summary>What a subscription pays for.</summary>
public enum SubscriptionKind
{
    /// <summary>The normal gym membership every member (including private trainers) pays.</summary>
    Membership = 0,

    /// <summary>The extra "private training service" a private trainer pays to train clients at the gym.</summary>
    PrivateTraining = 1,
}
