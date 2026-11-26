using Microsoft.AspNetCore.Identity;

namespace Gym.Infrastructure.Services;

/// <summary>Staff, optional logins (hashed passwords) and the club trainers' monthly ratings.</summary>
public sealed class StaffService(GymDbContext db, GymClock clock, IPasswordHasher<StaffMember> hasher)
{
    private const long SetupLockKey = 0x7469_7461_6E66_6974; // "titanfit"
    private static readonly StaffMember TimingDummy = new();
    private static readonly string TimingDummyHash = new PasswordHasher<StaffMember>().HashPassword(TimingDummy, "timing-dummy");

    /// <summary>Returns the staff member when the username and password are right and the login is active.</summary>
    public async Task<StaffMember?> VerifyLoginAsync(string? username, string? password, CancellationToken ct = default)
    {
        var normalized = StaffMember.NormalizeUsername(username);
        var staff = normalized is null ? null : await db.Staff.FirstOrDefaultAsync(s => s.Username == normalized, ct);
        if (staff is null || !staff.CanLogIn || string.IsNullOrEmpty(password))
        {
            // Same work as a real check, so a wrong username is not faster than a wrong password.
            hasher.VerifyHashedPassword(TimingDummy, TimingDummyHash, password ?? "");
            return null;
        }

        var result = hasher.VerifyHashedPassword(staff, staff.PasswordHash!, password);
        if (result == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            staff.PasswordHash = hasher.HashPassword(staff, password);
        }

        staff.LastLoginAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return staff;
    }

    /// <summary>True for the token check: the staff member still exists, is active and the token is not outdated.</summary>
    public Task<bool> IsTokenValidAsync(int staffId, Guid securityStamp, CancellationToken ct = default) =>
        db.Staff.AsNoTracking().AnyAsync(s => s.Id == staffId && s.IsActive && s.SecurityStamp == securityStamp, ct);

    public async Task<bool> NeedsSetupAsync(CancellationToken ct = default) =>
        !await db.Staff.AnyAsync(
            s => s.IsActive && s.Role == StaffRole.Admin && s.Username != null && s.PasswordHash != null, ct);

    /// <summary>First run: creates the first admin login. Refused once an admin exists.</summary>
    public async Task<StaffMember> SetupFirstAdminAsync(SetupAdminRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({SetupLockKey})", ct);
        if (!await NeedsSetupAsync(ct))
        {
            throw new ConflictException("already_set_up", "The first admin already exists. Log in instead.");
        }

        var admin = new StaffMember
        {
            FullName = Guard.Required(request.FullName, "Full name", StaffMember.NameMaxLength),
            Type = StaffType.Admin,
            CreatedAt = clock.UtcNow,
        };
        SetLogin(admin, request.Username, StaffRole.Admin);
        SetPassword(admin, request.Password);
        await EnsureUsernameFreeAsync(admin, ct);
        db.Staff.Add(admin);
        await db.SaveChangesOrConflictAsync(ct);
        await transaction.CommitAsync(ct);
        return admin;
    }

    public async Task<IReadOnlyList<StaffDto>> ListAsync(StaffType? type, bool includeInactive, CancellationToken ct = default)
    {
        var query = db.Staff.AsNoTracking().Where(s => includeInactive || s.IsActive);
        if (type is { } staffType)
        {
            query = query.Where(s => s.Type == staffType);
        }

        var staff = await query.OrderBy(s => s.FullName).ThenBy(s => s.Id).ToListAsync(ct);
        return staff.Select(ToDto).ToList();
    }

    public async Task<StaffDto> GetAsync(int id, CancellationToken ct = default) => ToDto(await FindAsync(id, ct));

    public async Task<StaffDto> CreateAsync(CreateStaffRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var staff = new StaffMember
        {
            FullName = Guard.Required(request.FullName, "Full name", StaffMember.NameMaxLength),
            Phone = Guard.Optional(PhoneNumber.Normalize(request.Phone), "Phone", Member.PhoneMaxLength),
            Type = Guard.Defined(request.Type, "Staff type"),
            CreatedAt = clock.UtcNow,
        };

        if (StaffMember.NormalizeUsername(request.Username) is not null)
        {
            SetLogin(staff, request.Username, request.Role ?? StaffMember.DefaultRoleFor(staff.Type));
            if (request.Password is not null)
            {
                SetPassword(staff, request.Password);
            }
        }
        else if (request.Role is not null || request.Password is not null)
        {
            throw new DomainException("username_required", "A login needs a username.");
        }

        await EnsureUsernameFreeAsync(staff, ct);
        db.Staff.Add(staff);
        await db.SaveChangesOrConflictAsync(ct);
        return ToDto(staff);
    }

    public async Task<StaffDto> UpdateAsync(int id, UpdateStaffRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var staff = await FindAsync(id, ct);
        var wasActiveAdmin = staff.IsActiveAdmin;

        staff.FullName = Guard.Required(request.FullName, "Full name", StaffMember.NameMaxLength);
        staff.Phone = Guard.Optional(PhoneNumber.Normalize(request.Phone), "Phone", Member.PhoneMaxLength);
        staff.Type = Guard.Defined(request.Type, "Staff type");
        if (staff.IsActive != request.IsActive)
        {
            staff.IsActive = request.IsActive;
            staff.RotateSecurityStamp();
        }

        if (StaffMember.NormalizeUsername(request.Username) is null)
        {
            if (staff.HasLogin || staff.PasswordHash is not null)
            {
                staff.Username = null;
                staff.Role = null;
                staff.PasswordHash = null;
                staff.RotateSecurityStamp();
            }
        }
        else
        {
            SetLogin(staff, request.Username, request.Role ?? staff.Role ?? StaffMember.DefaultRoleFor(staff.Type));
        }

        if (wasActiveAdmin && !staff.IsActiveAdmin)
        {
            await EnsureAnotherAdminAsync(id, ct);
        }

        await EnsureUsernameFreeAsync(staff, ct);
        await db.SaveChangesOrConflictAsync(ct);
        return ToDto(staff);
    }

    /// <summary>Admin sets a new password. The staff member's old tokens stop working.</summary>
    public async Task ResetPasswordAsync(int id, string? newPassword, CancellationToken ct = default)
    {
        var staff = await FindAsync(id, ct);
        if (!staff.HasLogin)
        {
            throw new DomainException("no_login", "Give this staff member a username first.");
        }

        SetPassword(staff, newPassword);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The logged-in user changes his own password. He logs in again afterwards.</summary>
    public async Task ChangeOwnPasswordAsync(int id, string? currentPassword, string? newPassword, CancellationToken ct = default)
    {
        var staff = await FindAsync(id, ct);
        if (staff.PasswordHash is null
            || hasher.VerifyHashedPassword(staff, staff.PasswordHash, currentPassword ?? "") == PasswordVerificationResult.Failed)
        {
            throw new DomainException("wrong_password", "The current password is not right.");
        }

        SetPassword(staff, newPassword);
        await db.SaveChangesAsync(ct);
    }

    public async Task<RatingDto> SetRatingAsync(int staffId, int year, int month, SetRatingRequest request, int ratedById, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var trainer = await FindAsync(staffId, ct);
        var now = clock.UtcNow;
        var rating = await db.StaffRatings.FirstOrDefaultAsync(r => r.StaffId == staffId && r.Year == year && r.Month == month, ct);
        if (rating is null)
        {
            rating = StaffRating.Create(trainer, year, month, request.Stars, request.Note, ratedById, now);
            db.StaffRatings.Add(rating);
        }
        else
        {
            StaffRating.EnsureCanBeRated(trainer);
            rating.Update(request.Stars, request.Note, ratedById, now);
        }

        await db.SaveChangesOrConflictAsync(ct);
        var id = rating.Id;
        return await ProjectRatings(db.StaffRatings.AsNoTracking().Where(r => r.Id == id)).SingleAsync(ct);
    }

    public async Task<IReadOnlyList<RatingDto>> GetRatingsAsync(int staffId, CancellationToken ct = default)
    {
        await FindAsync(staffId, ct);
        return await ProjectRatings(db.StaffRatings.AsNoTracking()
                .Where(r => r.StaffId == staffId)
                .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RatingDto>> GetMonthRatingsAsync(int year, int month, CancellationToken ct = default) =>
        await ProjectRatings(db.StaffRatings.AsNoTracking()
                .Where(r => r.Year == year && r.Month == month)
                .OrderBy(r => r.Staff!.FullName))
            .ToListAsync(ct);

    public static StaffDto ToDto(StaffMember s) =>
        new(s.Id, s.FullName, s.Phone, s.Type, s.IsActive, s.Username, s.Role, s.CanLogIn, s.CreatedAt, s.LastLoginAt);

    private static IQueryable<RatingDto> ProjectRatings(IQueryable<StaffRating> query) =>
        query.Select(r => new RatingDto(r.Id, r.StaffId, r.Staff!.FullName, r.Year, r.Month, r.Stars, r.Note, r.RatedBy!.FullName, r.UpdatedAt));

    private static void SetLogin(StaffMember staff, string? username, StaffRole? role)
    {
        var normalized = StaffMember.NormalizeUsername(username);
        if (!StaffMember.IsValidUsername(normalized))
        {
            throw new DomainException(
                "invalid_username",
                $"The username must be {StaffMember.UsernameMinLength} to {StaffMember.UsernameMaxLength} characters: a-z, 0-9, '.', '_' or '-'.");
        }

        if (role is not { } chosen)
        {
            throw new DomainException("role_required", "Choose a role for this login (Admin, Reception or Cashier).");
        }

        Guard.Defined(chosen, "Role");
        if (staff.Username != normalized || staff.Role != chosen)
        {
            staff.RotateSecurityStamp();
        }

        staff.Username = normalized;
        staff.Role = chosen;
    }

    private void SetPassword(StaffMember staff, string? password)
    {
        StaffMember.EnsureValidPassword(password);
        staff.PasswordHash = hasher.HashPassword(staff, password!);
        staff.RotateSecurityStamp();
    }

    private async Task EnsureUsernameFreeAsync(StaffMember staff, CancellationToken ct)
    {
        var username = staff.Username;
        if (username is not null && await db.Staff.AnyAsync(s => s.Username == username && s.Id != staff.Id, ct))
        {
            throw DbErrors.ToConflict("ux_staff_username");
        }
    }

    private async Task EnsureAnotherAdminAsync(int exceptId, CancellationToken ct)
    {
        var another = await db.Staff.AnyAsync(
            s => s.Id != exceptId && s.IsActive && s.Role == StaffRole.Admin && s.Username != null && s.PasswordHash != null, ct);
        if (!another)
        {
            throw new ConflictException("last_admin", "This is the last admin login. Create another admin first.");
        }
    }

    private async Task<StaffMember> FindAsync(int id, CancellationToken ct) =>
        await db.Staff.FindAsync([id], ct) ?? throw new NotFoundException("staff_not_found", $"Staff member {id} does not exist.");
}
