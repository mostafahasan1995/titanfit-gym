using System.Diagnostics.CodeAnalysis;
using Npgsql;

namespace Gym.Infrastructure.Persistence;

/// <summary>Turns PostgreSQL unique violations into <see cref="ConflictException"/> with a stable code.</summary>
public static class DbErrors
{
    public static bool IsUniqueViolation(DbUpdateException exception, [NotNullWhen(true)] out string? constraint)
    {
        ArgumentNullException.ThrowIfNull(exception);
        if (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
        {
            constraint = postgres.ConstraintName ?? "";
            return true;
        }

        constraint = null;
        return false;
    }

    public static ConflictException ToConflict(string? constraint) => constraint switch
    {
        "ux_members_national_no" => new("duplicate_national_no", "Another member already has this national number."),
        "ux_members_qr_code" => new("duplicate_qr_code", "This QR code is already in use. Try again."),
        "ux_members_member_no" => new("duplicate_member_no", "This member number is already in use."),
        "ux_products_barcode" => new("duplicate_barcode", "Another product already has this barcode."),
        "ux_staff_username" => new("duplicate_username", "This username is already taken."),
        "ux_staff_ratings_month" => new("duplicate_rating", "This month already has a rating. Try again."),
        "ux_shifts_open_per_cashier" => new("shift_already_open", "This cashier already has an open shift."),
        "ux_sales_number" => new("duplicate_sale_number", "This receipt number is already in use."),
        _ => new("duplicate", "The data conflicts with an existing record."),
    };

    /// <summary><see cref="DbContext.SaveChangesAsync(CancellationToken)"/> that reports duplicates as <see cref="ConflictException"/>.</summary>
    public static async Task<int> SaveChangesOrConflictAsync(this DbContext db, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        try
        {
            return await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, out var constraint))
        {
            throw ToConflict(constraint);
        }
    }
}
