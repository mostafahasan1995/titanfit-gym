using Gym.Infrastructure.Backups;
using Gym.Infrastructure.Services;
using Gym.Infrastructure.Storage;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Gym.Infrastructure;

public static class DependencyInjection
{
    /// <summary>Registers the database, the clock, the photo store and the data services.</summary>
    /// <param name="connectionString">Read when the first database context is created (so test hosts can override it).</param>
    public static IServiceCollection AddGymInfrastructure(this IServiceCollection services, Func<IServiceProvider, string?> connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        services.AddOptions();
        services.AddDbContext<GymDbContext>((sp, options) => GymDbContextOptions.Configure(options, connectionString(sp)));
        services.TryAddSingleton(new GymClock(TimeProvider.System));
        services.TryAddSingleton<IPasswordHasher<StaffMember>, PasswordHasher<StaffMember>>();
        services.TryAddSingleton<PhotoStore>();

        services.AddScoped<DatabaseInitializer>();
        services.AddScoped<SettingsService>();
        services.AddScoped<ExchangeRateService>();
        services.AddScoped<PlanService>();
        services.AddScoped<MemberService>();
        services.AddScoped<CheckInService>();
        services.AddScoped<StaffService>();
        services.AddScoped<ProductService>();
        services.AddScoped<StockService>();
        services.AddScoped<SalesService>();
        services.AddScoped<ShiftService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<ReportService>();
        return services;
    }

    /// <summary>
    /// Registers the database backups (<see cref="BackupService"/>, one for the server). Configure
    /// <see cref="BackupOptions"/> (section "Backup") and <see cref="StorageOptions"/> as well.
    /// </summary>
    /// <param name="connectionString">The database to back up, read at each run.</param>
    public static IServiceCollection AddGymBackups(this IServiceCollection services, Func<IServiceProvider, string?> connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        services.AddOptions<BackupOptions>();
        services.TryAddSingleton(sp => new BackupService(
            sp.GetRequiredService<IOptions<StorageOptions>>(),
            sp.GetRequiredService<IOptions<BackupOptions>>(),
            sp.GetRequiredService<GymClock>(),
            sp.GetRequiredService<ILogger<BackupService>>(),
            () => connectionString(sp)));
        return services;
    }
}
