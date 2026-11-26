namespace Gym.Domain;

/// <summary>
/// Monthly rating of a club trainer by management: 1 to 5 stars and a note. One per trainer per month;
/// it can be changed later. All months are kept as the history.
/// </summary>
public class StaffRating
{
    public const int MinStars = 1;
    public const int MaxStars = 5;
    public const int NoteMaxLength = 1000;

    public int Id { get; set; }

    public int StaffId { get; set; }

    public StaffMember? Staff { get; set; }

    public int Year { get; set; }

    /// <summary>1 to 12.</summary>
    public int Month { get; set; }

    public int Stars { get; set; }

    public string? Note { get; set; }

    public int RatedById { get; set; }

    public StaffMember? RatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public static StaffRating Create(StaffMember trainer, int year, int month, int stars, string? note, int ratedById, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(trainer);
        if (trainer.Type != StaffType.ClubTrainer)
        {
            throw new DomainException("not_a_club_trainer", "Only club trainers get a monthly rating.");
        }

        if (year is < 2000 or > 2100 || month is < 1 or > 12)
        {
            throw new DomainException("invalid_month", "The rating month is not valid.");
        }

        var rating = new StaffRating { StaffId = trainer.Id, Staff = trainer, Year = year, Month = month, CreatedAt = nowUtc };
        rating.Update(stars, note, ratedById, nowUtc);
        return rating;
    }

    public void Update(int stars, string? note, int ratedById, DateTime nowUtc)
    {
        if (stars is < MinStars or > MaxStars)
        {
            throw new DomainException("invalid_stars", $"The rating must be {MinStars} to {MaxStars} stars.");
        }

        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (note is { Length: > NoteMaxLength })
        {
            throw new DomainException("note_too_long", $"The note can be at most {NoteMaxLength} characters.");
        }

        Stars = stars;
        Note = note;
        RatedById = ratedById;
        UpdatedAt = nowUtc;
    }
}
