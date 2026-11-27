using Gym.Contracts;
using Gym.Domain;

namespace Gym.Client;

/// <summary>
/// Everything the apps can ask the server, one method per endpoint (docs/PLAN.md, "API overview").
/// <see cref="GymApiClient"/> talks to the real server; <see cref="Sample.SampleGymApi"/> answers from memory
/// (smoke screenshots in CI, no server needed). Errors throw <see cref="GymApiException"/>.
/// </summary>
public interface IGymApi
{
    /// <summary>The bearer token of the logged-in staff member, or null.</summary>
    string? AccessToken { get; set; }

    /// <summary>The server, e.g. <c>http://admin-pc:5080/</c>.</summary>
    Uri? BaseAddress { get; }

    /// <summary>
    /// A request sent with a token was refused with 401: the token was revoked (staff deactivated, password or
    /// role changed) or expired. The apps go back to the login screen. Raised on any thread.
    /// </summary>
    event EventHandler? SessionExpired;

    // Health, login, first run

    Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>Logs in and keeps the token for the next requests. Wrong login: 401 with code <c>invalid_login</c>.</summary>
    Task<LoginResponse> LoginAsync(string username, string password, CancellationToken ct = default);

    void Logout();

    Task<StaffDto> GetMeAsync(CancellationToken ct = default);

    Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default);

    Task<SetupStatusResponse> GetSetupStatusAsync(CancellationToken ct = default);

    /// <summary>First run: creates the first admin and logs in as him.</summary>
    Task<LoginResponse> SetupAdminAsync(SetupAdminRequest request, CancellationToken ct = default);

    // Members

    Task<PagedResult<MemberSummary>> SearchMembersAsync(
        string? query = null, SubscriptionStatus? status = null, MemberType? type = null, int page = 1, int pageSize = 20, CancellationToken ct = default);

    Task<MemberDto> GetMemberAsync(Guid id, CancellationToken ct = default);

    Task<MemberDto> CreateMemberAsync(CreateMemberRequest request, CancellationToken ct = default);

    Task<MemberDto> UpdateMemberAsync(Guid id, UpdateMemberRequest request, CancellationToken ct = default);

    Task<SubscriptionDto> RenewMemberAsync(Guid id, int planId, CancellationToken ct = default);

    Task<IReadOnlyList<SubscriptionDto>> GetMemberSubscriptionsAsync(Guid id, CancellationToken ct = default);

    /// <summary>Creates a new QR code for the member. The old one stops working.</summary>
    Task<QrCodeResponse> ReissueQrCodeAsync(Guid id, CancellationToken ct = default);

    /// <param name="contentType">image/jpeg or image/png.</param>
    Task UploadMemberPhotoAsync(Guid id, byte[] image, string contentType, CancellationToken ct = default);

    /// <summary>The member's photo, or null when he has none.</summary>
    Task<byte[]?> GetMemberPhotoAsync(Guid id, CancellationToken ct = default);

    /// <summary>Downloads a relative URL returned by the server, such as a <c>PhotoUrl</c>.</summary>
    Task<byte[]> GetBytesAsync(string relativeUrl, CancellationToken ct = default);

    // Check-in

    Task<CheckInResultDto> CheckInAsync(CheckInRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<CheckInResultDto>> GetCheckInsAsync(DateOnly? from = null, DateOnly? to = null, Guid? memberId = null, CancellationToken ct = default);

    // Plans

    Task<IReadOnlyList<PlanDto>> GetPlansAsync(bool includeInactive = false, CancellationToken ct = default);

    Task<PlanDto> CreatePlanAsync(SavePlanRequest request, CancellationToken ct = default);

    Task<PlanDto> UpdatePlanAsync(int id, SavePlanRequest request, CancellationToken ct = default);

    // Private trainers

    Task<IReadOnlyList<PrivateTrainerDto>> GetPrivateTrainersAsync(CancellationToken ct = default);

    Task<IReadOnlyList<MemberSummary>> GetPrivateTrainerClientsAsync(Guid trainerId, CancellationToken ct = default);

    Task LinkClientAsync(Guid trainerId, Guid memberId, CancellationToken ct = default);

    Task UnlinkClientAsync(Guid trainerId, Guid memberId, CancellationToken ct = default);

    /// <summary>Buys or renews the private training service (months × the fixed monthly fee).</summary>
    Task<SubscriptionDto> AddPrivateServiceAsync(Guid trainerId, int months, CancellationToken ct = default);

    // Staff and ratings

    Task<IReadOnlyList<StaffDto>> GetStaffAsync(StaffType? type = null, bool includeInactive = true, CancellationToken ct = default);

    Task<StaffDto> GetStaffMemberAsync(int id, CancellationToken ct = default);

    Task<StaffDto> CreateStaffAsync(CreateStaffRequest request, CancellationToken ct = default);

    Task<StaffDto> UpdateStaffAsync(int id, UpdateStaffRequest request, CancellationToken ct = default);

    Task ResetStaffPasswordAsync(int id, string newPassword, CancellationToken ct = default);

    Task<IReadOnlyList<RatingDto>> GetRatingsAsync(int staffId, CancellationToken ct = default);

    Task<RatingDto> SetRatingAsync(int staffId, int year, int month, SetRatingRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<RatingDto>> GetMonthRatingsAsync(int year, int month, CancellationToken ct = default);

    // Cafeteria: categories and products

    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(bool includeInactive = false, CancellationToken ct = default);

    Task<CategoryDto> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken ct = default);

    Task<CategoryDto> UpdateCategoryAsync(int id, SaveCategoryRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<ProductDto>> GetProductsAsync(string? query = null, int? categoryId = null, bool includeInactive = false, CancellationToken ct = default);

    Task<ProductDto> GetProductAsync(int id, CancellationToken ct = default);

    /// <summary>The product with this barcode, or null.</summary>
    Task<ProductDto?> FindProductByBarcodeAsync(string barcode, CancellationToken ct = default);

    Task<ProductDto> CreateProductAsync(SaveProductRequest request, CancellationToken ct = default);

    Task<ProductDto> UpdateProductAsync(int id, SaveProductRequest request, CancellationToken ct = default);

    // Cafeteria: stock

    Task<IReadOnlyList<StockLevelDto>> GetStockAsync(bool lowOnly = false, CancellationToken ct = default);

    Task<StockLevelDto> StockInAsync(StockInRequest request, CancellationToken ct = default);

    Task<StockLevelDto> AdjustStockAsync(StockAdjustmentRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<StockMovementDto>> GetStockMovementsAsync(int? productId = null, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default);

    // Cafeteria: sales and shifts

    Task<ReceiptDto> CreateSaleAsync(CreateSaleRequest request, CancellationToken ct = default);

    Task<ReceiptDto> GetSaleAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<SaleSummaryDto>> GetSalesAsync(DateOnly? from = null, DateOnly? to = null, int? cashierId = null, CancellationToken ct = default);

    Task<ShiftDto> OpenShiftAsync(OpenShiftRequest request, CancellationToken ct = default);

    /// <summary>The logged-in cashier's open shift, or null.</summary>
    Task<ShiftDto?> GetCurrentShiftAsync(CancellationToken ct = default);

    Task<ShiftSummaryDto> CloseShiftAsync(int id, CloseShiftRequest request, CancellationToken ct = default);

    Task<ShiftSummaryDto> GetShiftSummaryAsync(int id, CancellationToken ct = default);

    Task<IReadOnlyList<ShiftDto>> GetShiftsAsync(DateOnly? from = null, DateOnly? to = null, int? cashierId = null, CancellationToken ct = default);

    // Admin

    Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default);

    Task<SubscriptionReportDto> GetSubscriptionReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<AttendanceReportDto> GetAttendanceReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<SalesReportDto> GetSalesReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default);

    Task<SettingsDto> GetSettingsAsync(CancellationToken ct = default);

    Task<SettingsDto> UpdateSettingsAsync(SettingsDto settings, CancellationToken ct = default);

    // Backups (Admin)

    /// <summary>Last run, last good backup, next run and the warning to show.</summary>
    Task<BackupStatusDto> GetBackupStatusAsync(CancellationToken ct = default);

    /// <summary>The backup files in the backup folder, newest first.</summary>
    Task<IReadOnlyList<BackupFileDto>> GetBackupsAsync(CancellationToken ct = default);

    /// <summary>Backs up the database now. A failed run comes back with <c>Succeeded = false</c>; 409 <c>backup_running</c> while one runs.</summary>
    Task<BackupRunDto> RunBackupAsync(CancellationToken ct = default);

    Task<BackupSettingsDto> GetBackupSettingsAsync(CancellationToken ct = default);

    /// <summary>The folder must exist and be writable on the server PC (codes <c>backup_folder_missing</c>, <c>backup_folder_not_writable</c>).</summary>
    Task<BackupSettingsDto> UpdateBackupSettingsAsync(SaveBackupSettingsRequest request, CancellationToken ct = default);
}
