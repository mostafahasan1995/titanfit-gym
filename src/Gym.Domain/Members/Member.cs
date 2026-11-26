namespace Gym.Domain;

/// <summary>A gym member: ID card data + phone + photo + QR code.</summary>
public class Member
{
    public const int NameMaxLength = 100;
    public const int PhoneMaxLength = 32;
    public const int NationalNoMaxLength = 32;
    public const int NotesMaxLength = 1000;

    public Guid Id { get; set; }

    /// <summary>Short sequential display number (#00412), separate from <see cref="Id"/>. Set by a database sequence.</summary>
    public int MemberNo { get; set; }

    public MemberType Type { get; set; }

    // ID card fields (الاسم، اسم الأب، النسبة، اسم الأم، تاريخ الولادة، مكان الولادة، الرقم الوطني).
    public string FirstName { get; set; } = "";

    public string? FatherName { get; set; }

    public string LastName { get; set; } = "";

    public string? MotherName { get; set; }

    public DateOnly? BirthDate { get; set; }

    public string? BirthPlace { get; set; }

    /// <summary>Unique when present. Never put into the QR code.</summary>
    public string? NationalNo { get; set; }

    /// <summary>Stored without spaces or dashes, e.g. +963944123456.</summary>
    public string? Phone { get; set; }

    public string? Notes { get; set; }

    /// <summary>File name of the photo in the server's photo folder, or null.</summary>
    public string? PhotoFile { get; set; }

    /// <summary>Random code shown as the member's QR (see <see cref="MemberQrCode"/>). Unique.</summary>
    public string QrCode { get; set; } = "";

    /// <summary>The private trainer this member trains with, if any. Only a <see cref="MemberType.PrivateTrainer"/> can be set.</summary>
    public Guid? PrivateTrainerId { get; set; }

    public Member? PrivateTrainer { get; set; }

    public List<Member> Clients { get; set; } = [];

    public List<Subscription> Subscriptions { get; set; } = [];

    /// <summary>Normalized names for search (see <see cref="SearchText.Normalize"/>).</summary>
    public string SearchName { get; set; } = "";

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    /// <summary>First name, father's name and surname, e.g. "Ahmad Khaled Haddad".</summary>
    public string FullName => JoinNames(FirstName, FatherName, LastName);

    public static string JoinNames(params string?[] parts) =>
        string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()));

    public void RefreshSearchName() =>
        SearchName = SearchText.Normalize(JoinNames(FirstName, FatherName, LastName, MotherName));

    /// <summary>Creates a new random QR code. The old code stops working at once (nothing else matches it).</summary>
    public void ReissueQrCode() => QrCode = MemberQrCode.Generate();

    public void LinkToPrivateTrainer(Member trainer)
    {
        ArgumentNullException.ThrowIfNull(trainer);
        if (trainer.Type != MemberType.PrivateTrainer)
        {
            throw new DomainException(
                "not_a_private_trainer",
                $"Member {MemberNumber.Format(trainer.MemberNo)} is not a private trainer.");
        }

        if (trainer.Id == Id)
        {
            throw new DomainException("cannot_link_self", "A private trainer cannot be his own client.");
        }

        PrivateTrainerId = trainer.Id;
        PrivateTrainer = trainer;
    }

    public void UnlinkPrivateTrainer()
    {
        PrivateTrainerId = null;
        PrivateTrainer = null;
    }

    /// <summary>Changes the member type. A private trainer with linked clients must unlink them first.</summary>
    public void ChangeType(MemberType type, int linkedClientCount)
    {
        if (Type == MemberType.PrivateTrainer && type != MemberType.PrivateTrainer && linkedClientCount > 0)
        {
            throw new ConflictException(
                "trainer_has_clients",
                $"This private trainer still has {linkedClientCount} linked client(s). Unlink them first.");
        }

        Type = type;
    }
}
