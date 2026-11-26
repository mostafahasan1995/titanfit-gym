namespace Gym.Domain;

public enum MemberType
{
    Regular = 0,

    /// <summary>
    /// A member (player) who also trains his own clients at the gym. He pays the normal membership and,
    /// when he trains clients, the private training service. Not staff.
    /// </summary>
    PrivateTrainer = 1,
}
