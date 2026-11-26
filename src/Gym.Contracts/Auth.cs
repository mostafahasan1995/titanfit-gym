namespace Gym.Contracts;

public sealed record LoginRequest(string Username, string Password);

public sealed record LoginResponse(string Token, DateTime ExpiresAt, StaffDto Staff);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record SetupStatusResponse(bool NeedsSetup);

/// <summary>First-run setup: creates the first admin login. Only works while there is no admin.</summary>
public sealed record SetupAdminRequest(string FullName, string Username, string Password);
