namespace Gym.Api.Auth;

/// <summary>Authorization policies. Admin can do everything.</summary>
internal static class Policies
{
    public const string Admin = "Admin";

    /// <summary>Admin or Reception: members, check-in, private trainers.</summary>
    public const string Reception = "Reception";

    /// <summary>Admin or Cashier: products, stock, sales, shifts.</summary>
    public const string Cafeteria = "Cafeteria";

    /// <summary>Any logged-in staff member.</summary>
    public const string AnyStaff = "AnyStaff";
}

/// <summary>Claim names in the access token.</summary>
internal static class GymClaims
{
    public const string StaffId = "sub";
    public const string Name = "name";
    public const string Role = "role";
    public const string SecurityStamp = "stamp";
}
