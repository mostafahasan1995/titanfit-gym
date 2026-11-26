namespace Gym.Contracts;

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/// <summary>Status of a member's latest membership (or private training service).</summary>
public sealed record SubscriptionStatusDto(SubscriptionStatus Status, DateOnly EndDate, int DaysLeft, int DaysOverdue);

/// <summary>Body of an error response (RFC 7807 problem details with a stable <c>code</c>).</summary>
public sealed record ApiProblem(int? Status, string? Title, string? Code);
