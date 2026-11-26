namespace Gym.Contracts;

/// <summary>Role names in the access token. They match <see cref="Gym.Domain.StaffRole"/>.</summary>
public static class GymRoles
{
    public const string Admin = nameof(Domain.StaffRole.Admin);
    public const string Reception = nameof(Domain.StaffRole.Reception);
    public const string Cashier = nameof(Domain.StaffRole.Cashier);
}
