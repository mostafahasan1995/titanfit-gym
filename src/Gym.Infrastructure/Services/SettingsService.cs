namespace Gym.Infrastructure.Services;

public sealed class SettingsService(GymDbContext db, GymClock clock)
{
    public async Task<GymSettings> GetAsync(CancellationToken ct = default) =>
        await db.Settings.AsNoTracking().FirstOrDefaultAsync(s => s.Id == GymSettings.SingletonId, ct) ?? new GymSettings();

    public async Task<int> GetExpiringSoonDaysAsync(CancellationToken ct = default) => (await GetAsync(ct)).ExpiringSoonDays;

    public async Task<SettingsDto> GetDtoAsync(CancellationToken ct = default) => ToDto(await GetAsync(ct));

    public async Task<SettingsDto> UpdateAsync(SettingsDto request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var settings = await db.Settings.FirstOrDefaultAsync(s => s.Id == GymSettings.SingletonId, ct);
        if (settings is null)
        {
            settings = new GymSettings();
            db.Settings.Add(settings);
        }

        settings.Update(request.ExpiringSoonDays, request.PrivateTrainingMonthlyFee, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return ToDto(settings);
    }

    private static SettingsDto ToDto(GymSettings s) => new(s.ExpiringSoonDays, s.PrivateTrainingMonthlyFee);
}
