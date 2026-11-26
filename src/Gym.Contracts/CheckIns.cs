namespace Gym.Contracts;

/// <summary>Give either the scanned QR <see cref="Code"/> or the typed <see cref="MemberNo"/>.</summary>
public sealed record CheckInRequest(string Station, string? Code = null, int? MemberNo = null);

public sealed record CheckInResultDto(
    long Id,
    CheckInResult Result,
    bool Admitted,
    DateTime At,
    string Station,
    CheckInMethod Method,
    Guid? MemberId,
    int? MemberNo,
    string? MemberNoText,
    string? FullName,
    MemberType? MemberType,
    string? PhotoUrl,
    DateOnly? EndDate,
    int DaysLeft,
    int DaysOverdue,
    SubscriptionStatusDto? PrivateService);
