namespace Gym.Contracts;

public sealed record PlanDto(int Id, int Months, long Price, bool IsActive);

public sealed record SavePlanRequest(int Months, long Price, bool IsActive = true);
