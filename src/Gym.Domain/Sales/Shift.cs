namespace Gym.Domain;

/// <summary>A cashier's shift at a cafeteria station. Sales are linked to the cashier's open shift.</summary>
public class Shift
{
    public const int NoteMaxLength = 500;

    public int Id { get; set; }

    public int CashierId { get; set; }

    public StaffMember? Cashier { get; set; }

    public string Station { get; set; } = "";

    public DateTime OpenedAt { get; set; }

    public DateTime? ClosedAt { get; set; }

    /// <summary>Cash in the drawer at the start (whole SYP).</summary>
    public long OpeningCash { get; set; }

    /// <summary>Cash counted at the close, if entered.</summary>
    public long? ClosingCash { get; set; }

    public string? Note { get; set; }

    public bool IsOpen => ClosedAt is null;

    public void Close(long? closingCash, string? note, DateTime nowUtc)
    {
        if (!IsOpen)
        {
            throw new ConflictException("shift_closed", "This shift is already closed.");
        }

        if (closingCash is < 0)
        {
            throw new DomainException("invalid_amount", "The closing cash cannot be negative.");
        }

        ClosedAt = nowUtc;
        ClosingCash = closingCash;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
