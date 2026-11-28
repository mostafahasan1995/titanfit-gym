namespace Gym.Contracts;

public sealed record MemberDto(
    Guid Id,
    int MemberNo,
    string MemberNoText,
    MemberType Type,
    string FirstName,
    string? FatherName,
    string LastName,
    string? MotherName,
    string FullName,
    DateOnly? BirthDate,
    string? BirthPlace,
    string? NationalNo,
    string? Phone,
    string? Notes,
    string? PhotoUrl,
    string QrCode,
    Guid? PrivateTrainerId,
    string? PrivateTrainerName,
    SubscriptionStatusDto? Membership,
    SubscriptionStatusDto? PrivateService,
    DateTime CreatedAt);

public sealed record MemberSummary(
    Guid Id,
    int MemberNo,
    string MemberNoText,
    MemberType Type,
    string FullName,
    string? Phone,
    string? NationalNo,
    string? PhotoUrl,
    Guid? PrivateTrainerId,
    SubscriptionStatusDto? Membership);

/// <summary>New member. With a <see cref="PlanId"/> the first membership is paid at once.</summary>
public sealed record CreateMemberRequest(
    string FirstName,
    string LastName,
    string? FatherName = null,
    string? MotherName = null,
    DateOnly? BirthDate = null,
    string? BirthPlace = null,
    string? NationalNo = null,
    string? Phone = null,
    string? Notes = null,
    MemberType Type = MemberType.Regular,
    int? PlanId = null,
    Guid? PrivateTrainerId = null,
    PaymentRequest? Payment = null);

public sealed record UpdateMemberRequest(
    string FirstName,
    string LastName,
    string? FatherName = null,
    string? MotherName = null,
    DateOnly? BirthDate = null,
    string? BirthPlace = null,
    string? NationalNo = null,
    string? Phone = null,
    string? Notes = null,
    MemberType Type = MemberType.Regular);

public sealed record RenewRequest(int PlanId, PaymentRequest? Payment = null);

public sealed record QrCodeResponse(string QrCode);

public sealed record SubscriptionDto(
    int Id,
    Guid MemberId,
    SubscriptionKind Kind,
    int? PlanId,
    int Months,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal PricePaid,
    int CreatedById,
    string CreatedBy,
    DateTime CreatedAt,
    Currency Currency = Currency.Usd,
    decimal PaidAmount = 0,
    decimal? ExchangeRate = null);

public sealed record PrivateTrainerDto(
    Guid Id,
    int MemberNo,
    string MemberNoText,
    string FullName,
    string? Phone,
    string? PhotoUrl,
    int ClientCount,
    SubscriptionStatusDto? Membership,
    SubscriptionStatusDto? PrivateService);

/// <summary>Buys or renews the private training service: price = months × the monthly fee in the settings.</summary>
public sealed record PrivateServiceRequest(int Months, PaymentRequest? Payment = null);
