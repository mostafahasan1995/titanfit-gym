namespace Gym.Contracts;

public sealed record PlanDto(int Id, int Months, decimal Price, bool IsActive);

public sealed record SavePlanRequest(int Months, decimal Price, bool IsActive = true);
