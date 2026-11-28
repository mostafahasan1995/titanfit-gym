namespace Gym.Contracts;

public sealed record HourCount(int Hour, int Count);

public sealed record DayAmount(DateOnly Date, int Count, decimal Total);

public sealed record DayCount(DateOnly Date, int Count);

public sealed record DashboardDto(
    DateOnly Date,
    int CheckInsToday,
    int DeniedToday,
    IReadOnlyList<HourCount> CheckInsByHour,
    int ActiveMembers,
    int ExpiringSoonMembers,
    int ExpiredMembers,
    decimal RevenueSubscriptions,
    decimal RevenuePrivateTraining,
    decimal RevenueCafeteria,
    decimal RevenueTotal,
    IReadOnlyList<StockLevelDto> LowStock,
    IReadOnlyList<MemberSummary> ExpiringSoon);

public sealed record SubscriptionReportRow(SubscriptionKind Kind, int Months, int Count, decimal Total);

public sealed record StaffAmount(int StaffId, string StaffName, int Count, decimal Total);

public sealed record SubscriptionReportDto(
    DateOnly From,
    DateOnly To,
    int Count,
    decimal Total,
    IReadOnlyList<SubscriptionReportRow> ByPlan,
    IReadOnlyList<DayAmount> ByDay,
    IReadOnlyList<StaffAmount> ByStaff,
    CurrencyTotals? Paid = null);

public sealed record AttendanceReportDto(
    DateOnly From,
    DateOnly To,
    int TotalScans,
    int Admitted,
    int Denied,
    int UniqueMembers,
    IReadOnlyList<DayCount> ByDay,
    IReadOnlyList<HourCount> ByHour);

public sealed record SalesReportDto(
    DateOnly From,
    DateOnly To,
    int SaleCount,
    decimal Total,
    IReadOnlyList<DayAmount> ByDay,
    IReadOnlyList<ProductSalesDto> ByProduct,
    IReadOnlyList<StaffAmount> ByCashier,
    CurrencyTotals? Paid = null);

/// <summary>
/// The admin settings. <see cref="PrivateTrainingMonthlyFee"/> is in US dollars; amounts in Syrian pounds are rounded
/// to a multiple of <see cref="SypRoundingStep"/> pounds.
/// </summary>
public sealed record SettingsDto(int ExpiringSoonDays, decimal PrivateTrainingMonthlyFee, int SypRoundingStep = CurrencyRules.DefaultSypRoundingStep);
