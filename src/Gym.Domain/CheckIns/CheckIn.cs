namespace Gym.Domain;

/// <summary>How the member was identified at the door. Fingerprint and face come later (biometrics).</summary>
public enum CheckInMethod
{
    QrCode = 0,
    MemberNo = 1,
}

public enum CheckInResult
{
    Active = 0,
    ExpiringSoon = 1,
    Expired = 2,

    /// <summary>The member exists but never had a membership.</summary>
    NoSubscription = 3,

    /// <summary>No member has this QR code (or member number), e.g. an old QR after a reissue.</summary>
    UnknownMember = 4,
}

/// <summary>Every scan at reception is logged: member, time, station, result.</summary>
public class CheckIn
{
    public const int StationMaxLength = 64;
    public const int ScannedValueMaxLength = 64;

    public long Id { get; set; }

    /// <summary>Null when the scanned code matched no member.</summary>
    public Guid? MemberId { get; set; }

    public Member? Member { get; set; }

    public DateTime At { get; set; }

    public string Station { get; set; } = "";

    public CheckInMethod Method { get; set; }

    /// <summary>The scanned QR code or the typed member number, as received.</summary>
    public string? ScannedValue { get; set; }

    public CheckInResult Result { get; set; }

    public int DaysLeft { get; set; }

    public int DaysOverdue { get; set; }

    /// <summary>The staff member logged in at the station.</summary>
    public int? StaffId { get; set; }

    public StaffMember? Staff { get; set; }
}

public static class CheckInRules
{
    /// <param name="membership">The member's membership state, or null if he never had one.</param>
    public static CheckInResult ResultFor(SubscriptionState? membership) => membership?.Status switch
    {
        null => CheckInResult.NoSubscription,
        SubscriptionStatus.Active => CheckInResult.Active,
        SubscriptionStatus.ExpiringSoon => CheckInResult.ExpiringSoon,
        _ => CheckInResult.Expired,
    };

    /// <summary>Green or amber at the door. Expired, no subscription and unknown are red.</summary>
    public static bool IsAdmitted(CheckInResult result) =>
        result is CheckInResult.Active or CheckInResult.ExpiringSoon;
}
