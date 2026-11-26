namespace Gym.Infrastructure.Services;

public sealed class PlanService(GymDbContext db)
{
    public const int MaxMonths = 120;

    public async Task<IReadOnlyList<PlanDto>> ListAsync(bool includeInactive, CancellationToken ct = default) =>
        await db.Plans.AsNoTracking()
            .Where(p => includeInactive || p.IsActive)
            .OrderBy(p => p.Months).ThenBy(p => p.Id)
            .Select(p => new PlanDto(p.Id, p.Months, p.Price, p.IsActive))
            .ToListAsync(ct);

    public async Task<PlanDto> CreateAsync(SavePlanRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var plan = new Plan();
        Apply(plan, request);
        db.Plans.Add(plan);
        await db.SaveChangesAsync(ct);
        return ToDto(plan);
    }

    public async Task<PlanDto> UpdateAsync(int id, SavePlanRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var plan = await db.Plans.FindAsync([id], ct) ?? throw new NotFoundException("plan_not_found", $"Plan {id} does not exist.");
        Apply(plan, request);
        await db.SaveChangesAsync(ct);
        return ToDto(plan);
    }

    private static void Apply(Plan plan, SavePlanRequest request)
    {
        plan.Months = Guard.Range(request.Months, "Months", 1, MaxMonths);
        plan.Price = Guard.NotNegative(request.Price, "Price");
        plan.IsActive = request.IsActive;
    }

    private static PlanDto ToDto(Plan p) => new(p.Id, p.Months, p.Price, p.IsActive);
}
