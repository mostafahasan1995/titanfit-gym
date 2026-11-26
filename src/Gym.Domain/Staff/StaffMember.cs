namespace Gym.Domain;

/// <summary>The job of a staff member.</summary>
public enum StaffType
{
    Admin = 0,
    Reception = 1,
    Cashier = 2,

    /// <summary>A gym employee who trains members. Usually has no login. Rated monthly by management.</summary>
    ClubTrainer = 3,
}

/// <summary>What a login may do in the system (the API authorization roles).</summary>
public enum StaffRole
{
    Admin = 0,
    Reception = 1,
    Cashier = 2,
}

/// <summary>A gym employee. The login (username, password, role) is optional.</summary>
public class StaffMember
{
    public const int NameMaxLength = 100;
    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 32;
    public const int PasswordMinLength = 6;
    public const int PasswordMaxLength = 128;

    public int Id { get; set; }

    public string FullName { get; set; } = "";

    public string? Phone { get; set; }

    public StaffType Type { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Lower case. Null when the staff member has no login.</summary>
    public string? Username { get; set; }

    public string? PasswordHash { get; set; }

    public StaffRole? Role { get; set; }

    /// <summary>Changes when the password, role, username or active flag changes; older tokens stop working.</summary>
    public Guid SecurityStamp { get; set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; set; }

    public DateTime? LastLoginAt { get; set; }

    public bool HasLogin => Username is not null && Role is not null;

    public bool CanLogIn => IsActive && HasLogin && PasswordHash is not null;

    public bool IsActiveAdmin => CanLogIn && Role == StaffRole.Admin;

    /// <summary>The role a login gets when none is chosen: the same as the job. Club trainers need an explicit role.</summary>
    public static StaffRole? DefaultRoleFor(StaffType type) => type switch
    {
        StaffType.Admin => StaffRole.Admin,
        StaffType.Reception => StaffRole.Reception,
        StaffType.Cashier => StaffRole.Cashier,
        _ => null,
    };

    /// <summary>Trims and lower-cases; returns null for an empty value.</summary>
    public static string? NormalizeUsername(string? username) =>
        string.IsNullOrWhiteSpace(username) ? null : username.Trim().ToLowerInvariant();

    /// <summary>3 to 32 characters: a-z, 0-9, '.', '_' or '-'.</summary>
    public static bool IsValidUsername(string? normalized) =>
        normalized is { Length: >= UsernameMinLength and <= UsernameMaxLength }
        && normalized.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c is '.' or '_' or '-');

    public static void EnsureValidPassword(string? password)
    {
        if (password is null || password.Length < PasswordMinLength || password.Length > PasswordMaxLength)
        {
            throw new DomainException(
                "invalid_password",
                $"The password must be {PasswordMinLength} to {PasswordMaxLength} characters long.");
        }
    }

    public void RotateSecurityStamp() => SecurityStamp = Guid.NewGuid();
}
