namespace Gym.Contracts;

public sealed record StaffDto(
    int Id,
    string FullName,
    string? Phone,
    StaffType Type,
    bool IsActive,
    string? Username,
    StaffRole? Role,
    bool CanLogIn,
    DateTime CreatedAt,
    DateTime? LastLoginAt);

/// <summary>New staff member. The login is optional: leave <see cref="Username"/> empty for none.</summary>
public sealed record CreateStaffRequest(
    string FullName,
    StaffType Type,
    string? Phone = null,
    string? Username = null,
    string? Password = null,
    StaffRole? Role = null);

/// <summary>An empty <see cref="Username"/> removes the login.</summary>
public sealed record UpdateStaffRequest(
    string FullName,
    StaffType Type,
    bool IsActive = true,
    string? Phone = null,
    string? Username = null,
    StaffRole? Role = null);

public sealed record ResetPasswordRequest(string NewPassword);

public sealed record RatingDto(
    int Id,
    int StaffId,
    string StaffName,
    int Year,
    int Month,
    int Stars,
    string? Note,
    string RatedBy,
    DateTime UpdatedAt);

public sealed record SetRatingRequest(int Stars, string? Note = null);
