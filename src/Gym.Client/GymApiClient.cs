using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Gym.Contracts;
using Gym.Domain;

namespace Gym.Client;

/// <summary>
/// Typed client for the Gym.Api server. The apps talk only to the API, never to the database.
/// Set <see cref="HttpClient.BaseAddress"/> to the server, e.g. <c>http://admin-pc:5080/</c>. After
/// <see cref="LoginAsync"/> the token is sent with every request. Errors throw <see cref="GymApiException"/>.
/// </summary>
public sealed class GymApiClient(HttpClient http)
{
    /// <summary>The bearer token of the logged-in staff member, or null.</summary>
    public string? AccessToken { get; set; }

    public Uri? BaseAddress => http.BaseAddress;

    // Health and login

    /// <summary>Calls <c>GET /health</c>.</summary>
    public async Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var health = await http.GetFromJsonAsync<HealthResponse>("health", GymJson.Options, cancellationToken).ConfigureAwait(false);
        return health ?? throw new InvalidOperationException("The server returned an empty health response.");
    }

    public async Task<LoginResponse> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var login = await PostAsync<LoginResponse>("api/auth/login", new LoginRequest(username, password), ct).ConfigureAwait(false);
        AccessToken = login.Token;
        return login;
    }

    public void Logout() => AccessToken = null;

    public Task<StaffDto> GetMeAsync(CancellationToken ct = default) => GetAsync<StaffDto>("api/auth/me", ct);

    /// <summary>Changes the logged-in user's password. Log in again afterwards.</summary>
    public Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, "api/auth/change-password", new ChangePasswordRequest(currentPassword, newPassword), ct);

    public Task<SetupStatusResponse> GetSetupStatusAsync(CancellationToken ct = default) => GetAsync<SetupStatusResponse>("api/setup/status", ct);

    /// <summary>First run: creates the first admin and logs in as him.</summary>
    public async Task<LoginResponse> SetupAdminAsync(SetupAdminRequest request, CancellationToken ct = default)
    {
        var login = await PostAsync<LoginResponse>("api/setup/admin", request, ct).ConfigureAwait(false);
        AccessToken = login.Token;
        return login;
    }

    // Members

    public Task<PagedResult<MemberSummary>> SearchMembersAsync(
        string? query = null, SubscriptionStatus? status = null, MemberType? type = null, int page = 1, int pageSize = 20, CancellationToken ct = default) =>
        GetAsync<PagedResult<MemberSummary>>("api/members" + Query(("q", query), ("status", status), ("type", type), ("page", page), ("pageSize", pageSize)), ct);

    public Task<MemberDto> GetMemberAsync(Guid id, CancellationToken ct = default) => GetAsync<MemberDto>($"api/members/{id}", ct);

    public Task<MemberDto> CreateMemberAsync(CreateMemberRequest request, CancellationToken ct = default) => PostAsync<MemberDto>("api/members", request, ct);

    public Task<MemberDto> UpdateMemberAsync(Guid id, UpdateMemberRequest request, CancellationToken ct = default) =>
        SendAsync<MemberDto>(HttpMethod.Put, $"api/members/{id}", request, ct);

    public Task<SubscriptionDto> RenewMemberAsync(Guid id, int planId, CancellationToken ct = default) =>
        PostAsync<SubscriptionDto>($"api/members/{id}/renew", new RenewRequest(planId), ct);

    public Task<IReadOnlyList<SubscriptionDto>> GetMemberSubscriptionsAsync(Guid id, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<SubscriptionDto>>($"api/members/{id}/subscriptions", ct);

    /// <summary>Creates a new QR code for the member. The old one stops working.</summary>
    public Task<QrCodeResponse> ReissueQrCodeAsync(Guid id, CancellationToken ct = default) =>
        PostAsync<QrCodeResponse>($"api/members/{id}/qr-code", null, ct);

    /// <param name="contentType">image/jpeg or image/png.</param>
    public async Task UploadMemberPhotoAsync(Guid id, byte[] image, string contentType, CancellationToken ct = default)
    {
        using var content = new ByteArrayContent(image);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var response = await SendCoreAsync(HttpMethod.Put, $"api/members/{id}/photo", content, ct).ConfigureAwait(false);
    }

    /// <summary>The member's photo, or null when he has none.</summary>
    public async Task<byte[]?> GetMemberPhotoAsync(Guid id, CancellationToken ct = default)
    {
        try
        {
            return await GetBytesAsync($"api/members/{id}/photo", ct).ConfigureAwait(false);
        }
        catch (GymApiException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>Downloads a relative URL returned by the server, such as a <c>PhotoUrl</c>.</summary>
    public async Task<byte[]> GetBytesAsync(string relativeUrl, CancellationToken ct = default)
    {
        using var response = await SendCoreAsync(HttpMethod.Get, relativeUrl, null, ct).ConfigureAwait(false);
        return await response.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    // Check-in

    public Task<CheckInResultDto> CheckInAsync(CheckInRequest request, CancellationToken ct = default) =>
        PostAsync<CheckInResultDto>("api/check-ins", request, ct);

    public Task<IReadOnlyList<CheckInResultDto>> GetCheckInsAsync(DateOnly? from = null, DateOnly? to = null, Guid? memberId = null, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<CheckInResultDto>>("api/check-ins" + Query(("from", from), ("to", to), ("memberId", memberId)), ct);

    // Plans

    public Task<IReadOnlyList<PlanDto>> GetPlansAsync(bool includeInactive = false, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<PlanDto>>("api/plans" + Query(("includeInactive", includeInactive)), ct);

    public Task<PlanDto> CreatePlanAsync(SavePlanRequest request, CancellationToken ct = default) => PostAsync<PlanDto>("api/plans", request, ct);

    public Task<PlanDto> UpdatePlanAsync(int id, SavePlanRequest request, CancellationToken ct = default) =>
        SendAsync<PlanDto>(HttpMethod.Put, $"api/plans/{id}", request, ct);

    // Private trainers

    public Task<IReadOnlyList<PrivateTrainerDto>> GetPrivateTrainersAsync(CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<PrivateTrainerDto>>("api/private-trainers", ct);

    public Task<IReadOnlyList<MemberSummary>> GetPrivateTrainerClientsAsync(Guid trainerId, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<MemberSummary>>($"api/private-trainers/{trainerId}/clients", ct);

    public Task LinkClientAsync(Guid trainerId, Guid memberId, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Put, $"api/private-trainers/{trainerId}/clients/{memberId}", null, ct);

    public Task UnlinkClientAsync(Guid trainerId, Guid memberId, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, $"api/private-trainers/{trainerId}/clients/{memberId}", null, ct);

    /// <summary>Buys or renews the private training service (months × the fixed monthly fee).</summary>
    public Task<SubscriptionDto> AddPrivateServiceAsync(Guid trainerId, int months, CancellationToken ct = default) =>
        PostAsync<SubscriptionDto>($"api/private-trainers/{trainerId}/service", new PrivateServiceRequest(months), ct);

    // Staff and ratings

    public Task<IReadOnlyList<StaffDto>> GetStaffAsync(StaffType? type = null, bool includeInactive = true, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<StaffDto>>("api/staff" + Query(("type", type), ("includeInactive", includeInactive)), ct);

    public Task<StaffDto> GetStaffMemberAsync(int id, CancellationToken ct = default) => GetAsync<StaffDto>($"api/staff/{id}", ct);

    public Task<StaffDto> CreateStaffAsync(CreateStaffRequest request, CancellationToken ct = default) => PostAsync<StaffDto>("api/staff", request, ct);

    public Task<StaffDto> UpdateStaffAsync(int id, UpdateStaffRequest request, CancellationToken ct = default) =>
        SendAsync<StaffDto>(HttpMethod.Put, $"api/staff/{id}", request, ct);

    public Task ResetStaffPasswordAsync(int id, string newPassword, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, $"api/staff/{id}/password", new ResetPasswordRequest(newPassword), ct);

    public Task<IReadOnlyList<RatingDto>> GetRatingsAsync(int staffId, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<RatingDto>>($"api/staff/{staffId}/ratings", ct);

    public Task<RatingDto> SetRatingAsync(int staffId, int year, int month, SetRatingRequest request, CancellationToken ct = default) =>
        SendAsync<RatingDto>(HttpMethod.Put, FormattableString.Invariant($"api/staff/{staffId}/ratings/{year}/{month}"), request, ct);

    public Task<IReadOnlyList<RatingDto>> GetMonthRatingsAsync(int year, int month, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<RatingDto>>("api/staff/ratings" + Query(("year", year), ("month", month)), ct);

    // Cafeteria: categories and products

    public Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(bool includeInactive = false, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<CategoryDto>>("api/product-categories" + Query(("includeInactive", includeInactive)), ct);

    public Task<CategoryDto> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken ct = default) =>
        PostAsync<CategoryDto>("api/product-categories", request, ct);

    public Task<CategoryDto> UpdateCategoryAsync(int id, SaveCategoryRequest request, CancellationToken ct = default) =>
        SendAsync<CategoryDto>(HttpMethod.Put, $"api/product-categories/{id}", request, ct);

    public Task<IReadOnlyList<ProductDto>> GetProductsAsync(string? query = null, int? categoryId = null, bool includeInactive = false, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<ProductDto>>("api/products" + Query(("q", query), ("categoryId", categoryId), ("includeInactive", includeInactive)), ct);

    public Task<ProductDto> GetProductAsync(int id, CancellationToken ct = default) => GetAsync<ProductDto>($"api/products/{id}", ct);

    /// <summary>The product with this barcode, or null.</summary>
    public async Task<ProductDto?> FindProductByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        try
        {
            return await GetAsync<ProductDto>($"api/products/by-barcode/{Uri.EscapeDataString(barcode)}", ct).ConfigureAwait(false);
        }
        catch (GymApiException e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public Task<ProductDto> CreateProductAsync(SaveProductRequest request, CancellationToken ct = default) => PostAsync<ProductDto>("api/products", request, ct);

    public Task<ProductDto> UpdateProductAsync(int id, SaveProductRequest request, CancellationToken ct = default) =>
        SendAsync<ProductDto>(HttpMethod.Put, $"api/products/{id}", request, ct);

    // Cafeteria: stock

    public Task<IReadOnlyList<StockLevelDto>> GetStockAsync(bool lowOnly = false, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<StockLevelDto>>("api/stock" + Query(("lowOnly", lowOnly)), ct);

    public Task<StockLevelDto> StockInAsync(StockInRequest request, CancellationToken ct = default) => PostAsync<StockLevelDto>("api/stock/in", request, ct);

    public Task<StockLevelDto> AdjustStockAsync(StockAdjustmentRequest request, CancellationToken ct = default) =>
        PostAsync<StockLevelDto>("api/stock/adjustments", request, ct);

    public Task<IReadOnlyList<StockMovementDto>> GetStockMovementsAsync(int? productId = null, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<StockMovementDto>>("api/stock/movements" + Query(("productId", productId), ("from", from), ("to", to)), ct);

    // Cafeteria: sales and shifts

    public Task<ReceiptDto> CreateSaleAsync(CreateSaleRequest request, CancellationToken ct = default) => PostAsync<ReceiptDto>("api/sales", request, ct);

    public Task<ReceiptDto> GetSaleAsync(long id, CancellationToken ct = default) =>
        GetAsync<ReceiptDto>(FormattableString.Invariant($"api/sales/{id}"), ct);

    public Task<IReadOnlyList<SaleSummaryDto>> GetSalesAsync(DateOnly? from = null, DateOnly? to = null, int? cashierId = null, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<SaleSummaryDto>>("api/sales" + Query(("from", from), ("to", to), ("cashierId", cashierId)), ct);

    public Task<ShiftDto> OpenShiftAsync(OpenShiftRequest request, CancellationToken ct = default) => PostAsync<ShiftDto>("api/shifts/open", request, ct);

    /// <summary>The logged-in cashier's open shift, or null.</summary>
    public async Task<ShiftDto?> GetCurrentShiftAsync(CancellationToken ct = default)
    {
        using var response = await SendCoreAsync(HttpMethod.Get, "api/shifts/current", null, ct).ConfigureAwait(false);
        return response.StatusCode == HttpStatusCode.NoContent
            ? null
            : await response.Content.ReadFromJsonAsync<ShiftDto>(GymJson.Options, ct).ConfigureAwait(false);
    }

    public Task<ShiftSummaryDto> CloseShiftAsync(int id, CloseShiftRequest request, CancellationToken ct = default) =>
        PostAsync<ShiftSummaryDto>($"api/shifts/{id}/close", request, ct);

    public Task<ShiftSummaryDto> GetShiftSummaryAsync(int id, CancellationToken ct = default) =>
        GetAsync<ShiftSummaryDto>($"api/shifts/{id}/summary", ct);

    public Task<IReadOnlyList<ShiftDto>> GetShiftsAsync(DateOnly? from = null, DateOnly? to = null, int? cashierId = null, CancellationToken ct = default) =>
        GetAsync<IReadOnlyList<ShiftDto>>("api/shifts" + Query(("from", from), ("to", to), ("cashierId", cashierId)), ct);

    // Admin

    public Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default) => GetAsync<DashboardDto>("api/dashboard", ct);

    public Task<SubscriptionReportDto> GetSubscriptionReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        GetAsync<SubscriptionReportDto>("api/reports/subscriptions" + Query(("from", from), ("to", to)), ct);

    public Task<AttendanceReportDto> GetAttendanceReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        GetAsync<AttendanceReportDto>("api/reports/attendance" + Query(("from", from), ("to", to)), ct);

    public Task<SalesReportDto> GetSalesReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default) =>
        GetAsync<SalesReportDto>("api/reports/sales" + Query(("from", from), ("to", to)), ct);

    public Task<SettingsDto> GetSettingsAsync(CancellationToken ct = default) => GetAsync<SettingsDto>("api/settings", ct);

    public Task<SettingsDto> UpdateSettingsAsync(SettingsDto settings, CancellationToken ct = default) =>
        SendAsync<SettingsDto>(HttpMethod.Put, "api/settings", settings, ct);

    // Plumbing

    private Task<T> GetAsync<T>(string url, CancellationToken ct) => SendAsync<T>(HttpMethod.Get, url, null, ct);

    private Task<T> PostAsync<T>(string url, object? body, CancellationToken ct) => SendAsync<T>(HttpMethod.Post, url, body, ct);

    private async Task<T> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var content = body is null ? null : JsonContent.Create(body, body.GetType(), options: GymJson.Options);
        using var response = await SendCoreAsync(method, url, content, ct).ConfigureAwait(false);
        var result = await response.Content.ReadFromJsonAsync<T>(GymJson.Options, ct).ConfigureAwait(false);
        return result ?? throw new GymApiException(response.StatusCode, "empty_response", "The server returned an empty response.");
    }

    private async Task SendAsync(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var content = body is null ? null : JsonContent.Create(body, body.GetType(), options: GymJson.Options);
        using var response = await SendCoreAsync(method, url, content, ct).ConfigureAwait(false);
    }

    private async Task<HttpResponseMessage> SendCoreAsync(HttpMethod method, string url, HttpContent? content, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url) { Content = content };
        if (!string.IsNullOrEmpty(AccessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AccessToken);
        }

        var response = await http.SendAsync(request, ct).ConfigureAwait(false);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        using (response)
        {
            throw await GymApiException.FromResponseAsync(response, ct).ConfigureAwait(false);
        }
    }

    private static string Query(params (string Name, object? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => p.Value is not null)
            .Select(p => Uri.EscapeDataString(p.Name) + "=" + Uri.EscapeDataString(Format(p.Value!)))
            .ToList();
        return parts.Count == 0 ? "" : "?" + string.Join("&", parts);
    }

    private static string Format(object value) => value switch
    {
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}

/// <summary>Body of <c>GET /health</c>.</summary>
public sealed record HealthResponse(string Status, string Version);
