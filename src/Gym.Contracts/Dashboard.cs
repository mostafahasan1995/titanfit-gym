namespace Gym.Contracts;

public sealed record HourCount(int Hour, int Count);

public sealed record DayAmount(DateOnly Date, int Count, long Total);

public sealed record DayCount(DateOnly Date, int Count);

public sealed record DashboardDto(
    DateOnly Date,
    int CheckInsToday,
    int DeniedToday,
    IReadOnlyList<HourCount> CheckInsByHour,
    int ActiveMembers,
    int ExpiringSoonMembers,
    int ExpiredMembers,
    long RevenueSubscriptions,
    long RevenuePrivateTraining,
    long RevenueCafeteria,
    long RevenueTotal,
    IReadOnlyList<StockLevelDto> LowStock,
    IReadOnlyList<MemberSummary> ExpiringSoon);

public sealed record SubscriptionReportRow(SubscriptionKind Kind, int Months, int Count, long Total);

public sealed record StaffAmount(int StaffId, string StaffName, int Count, long Total);

public sealed record SubscriptionReportDto(
    DateOnly From,
    DateOnly To,
    int Count,
    long Total,
    IReadOnlyList<SubscriptionReportRow> ByPlan,
    IReadOnlyList<DayAmount> ByDay,
    IReadOnlyList<StaffAmount> ByStaff);

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
    long Total,
    IReadOnlyList<DayAmount> ByDay,
    IReadOnlyList<ProductSalesDto> ByProduct,
    IReadOnlyList<StaffAmount> ByCashier);

public sealed record SettingsDto(int ExpiringSoonDays, long PrivateTrainingMonthlyFee);
