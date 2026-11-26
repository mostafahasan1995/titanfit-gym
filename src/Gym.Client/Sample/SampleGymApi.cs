using System.Globalization;
using System.Net;
using Gym.Contracts;
using Gym.Domain;

namespace Gym.Client.Sample;

/// <summary>
/// An in-memory <see cref="IGymApi"/> with realistic sample data (members in every status, plans, staff and ratings,
/// private trainers with clients, products in the 4 categories with stock and low-stock items, sales, shifts,
/// check-ins). Used by the smoke screenshots so CI renders real screens without a server.
/// <para>
/// Reads follow the server's rules (status from <see cref="SubscriptionRules.GetStatus"/>, stock = sum of movements).
/// Writes change the data in memory and publish the live events to <see cref="Live"/>. Roles are not checked.
/// Every login uses the password <see cref="SamplePassword"/>. Not thread-safe: call it from the UI thread.
/// </para>
/// </summary>
public sealed partial class SampleGymApi : IGymApi
{
    public const string SamplePassword = "titanfit";

    private readonly List<PlanDto> plans = [];
    private readonly List<StaffDto> staff = [];
    private readonly Dictionary<int, string> passwords = [];
    private readonly List<RatingDto> ratings = [];
    private readonly List<MemberRow> members = [];
    private readonly Dictionary<Guid, byte[]> photos = [];
    private readonly List<SubscriptionDto> subscriptions = [];
    private readonly List<CheckInResultDto> checkIns = [];
    private readonly List<CategoryDto> categories = [];
    private readonly List<ProductRow> products = [];
    private readonly List<StockMovementDto> movements = [];
    private readonly List<ReceiptDto> sales = [];
    private readonly List<ShiftDto> shifts = [];
    private SettingsDto settings = new(SubscriptionRules.DefaultExpiringSoonDays, 100_000);
    private StaffDto? current;
    private int nextId = 10_000;

    /// <param name="today">The local day the sample data is built around (default: today).</param>
    /// <param name="live">Receives the live events of the changes made through this API.</param>
    public SampleGymApi(DateOnly? today = null, SampleLiveConnection? live = null)
    {
        Today = today ?? DateOnly.FromDateTime(DateTime.Now);
        Live = live ?? new SampleLiveConnection();
        Seed();
    }

    /// <summary>The local day the data is built around. "Today" for statuses, the dashboard and the open shift.</summary>
    public DateOnly Today { get; }

    public SampleLiveConnection Live { get; }

    /// <summary>Makes <see cref="GetSetupStatusAsync"/> report a server without an admin (first run).</summary>
    public bool NeedsSetup { get; set; }

    public string? AccessToken { get; set; }

    public Uri? BaseAddress { get; } = new("http://sample.titanfit.local/");

    public event EventHandler? SessionExpired;

    /// <summary>The logged-in staff member, or null.</summary>
    public StaffDto? CurrentStaff => current;

    /// <summary>The sample username for a role: admin, sara (reception) or ahmad (cashier).</summary>
    public static string UsernameFor(StaffRole role) => role switch
    {
        StaffRole.Admin => "admin",
        StaffRole.Reception => "sara",
        StaffRole.Cashier => "ahmad",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    /// <summary>Acts like the server revoked the token: raises <see cref="SessionExpired"/>.</summary>
    public void ExpireSession()
    {
        AccessToken = null;
        current = null;
        SessionExpired?.Invoke(this, EventArgs.Empty);
    }

    // Health, login, first run

    public Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new HealthResponse("ok", "0.1.0-sample"));

    public Task<LoginResponse> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var user = staff.FirstOrDefault(s => s.CanLogIn && s.IsActive && string.Equals(s.Username, username?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (user is null || password != passwords.GetValueOrDefault(user.Id, SamplePassword))
        {
            throw new GymApiException(HttpStatusCode.Unauthorized, "invalid_login", "Wrong username or password.");
        }

        return Task.FromResult(LogIn(user));
    }

    public void Logout()
    {
        AccessToken = null;
        current = null;
    }

    public Task<StaffDto> GetMeAsync(CancellationToken ct = default) =>
        Task.FromResult(current ?? throw new GymApiException(HttpStatusCode.Unauthorized, null, "Not logged in."));

    public Task ChangePasswordAsync(string currentPassword, string newPassword, CancellationToken ct = default)
    {
        var me = current ?? throw new GymApiException(HttpStatusCode.Unauthorized, null, "Not logged in.");
        if (currentPassword != passwords.GetValueOrDefault(me.Id, SamplePassword))
        {
            throw Bad("wrong_password", "The current password is not right.");
        }

        passwords[me.Id] = newPassword;
        return Task.CompletedTask;
    }

    public Task<SetupStatusResponse> GetSetupStatusAsync(CancellationToken ct = default) => Task.FromResult(new SetupStatusResponse(NeedsSetup));

    public Task<LoginResponse> SetupAdminAsync(SetupAdminRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!NeedsSetup)
        {
            throw new GymApiException(HttpStatusCode.Conflict, "already_set_up", "An admin already exists.");
        }

        NeedsSetup = false;
        var admin = new StaffDto(NextId(), request.FullName, null, StaffType.Admin, true, request.Username, StaffRole.Admin, true, DateTime.UtcNow, null);
        staff.Add(admin);
        passwords[admin.Id] = request.Password;
        return Task.FromResult(LogIn(admin));
    }

    // Members

    public Task<PagedResult<MemberSummary>> SearchMembersAsync(
        string? query = null, SubscriptionStatus? status = null, MemberType? type = null, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        IEnumerable<MemberRow> rows = members;
        if (query is { } text && !string.IsNullOrWhiteSpace(text))
        {
            rows = rows.Where(m => Matches(m, text));
        }

        if (type is { } t)
        {
            rows = rows.Where(m => m.Type == t);
        }

        if (status is { } s)
        {
            rows = rows.Where(m => StatusOf(m.Id, SubscriptionKind.Membership)?.Status == s);
        }

        var list = rows.OrderByDescending(m => m.MemberNo).ToList();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).Select(ToSummary).ToList();
        return Task.FromResult(new PagedResult<MemberSummary>(items, list.Count, page, pageSize));
    }

    public Task<MemberDto> GetMemberAsync(Guid id, CancellationToken ct = default) => Task.FromResult(ToDto(Member(id)));

    public Task<MemberDto> CreateMemberAsync(CreateMemberRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CheckNationalNo(request.NationalNo, null);
        var row = new MemberRow
        {
            Id = Guid.NewGuid(),
            MemberNo = members.Count == 0 ? 1 : members.Max(m => m.MemberNo) + 1,
            Type = request.Type,
            FirstName = request.FirstName.Trim(),
            FatherName = request.FatherName,
            LastName = request.LastName.Trim(),
            MotherName = request.MotherName,
            BirthDate = request.BirthDate,
            BirthPlace = request.BirthPlace,
            NationalNo = request.NationalNo,
            Phone = request.Phone?.Replace(" ", "", StringComparison.Ordinal),
            Notes = request.Notes,
            QrCode = NewQrCode(),
            PrivateTrainerId = request.PrivateTrainerId,
            CreatedAt = DateTime.UtcNow,
        };
        members.Add(row);
        if (request.PlanId is { } planId)
        {
            AddMembership(row.Id, Plan(planId), DateTime.UtcNow);
        }

        Live.PublishMemberChanged(new MemberChangedEvent(row.Id, MemberChangeKind.Created));
        return Task.FromResult(ToDto(row));
    }

    public Task<MemberDto> UpdateMemberAsync(Guid id, UpdateMemberRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var row = Member(id);
        CheckNationalNo(request.NationalNo, id);
        if (row.Type == MemberType.PrivateTrainer && request.Type == MemberType.Regular && members.Any(m => m.PrivateTrainerId == id))
        {
            throw Bad("trainer_has_clients", "Unlink the clients first.");
        }

        row.FirstName = request.FirstName.Trim();
        row.LastName = request.LastName.Trim();
        row.FatherName = request.FatherName;
        row.MotherName = request.MotherName;
        row.BirthDate = request.BirthDate;
        row.BirthPlace = request.BirthPlace;
        row.NationalNo = request.NationalNo;
        row.Phone = request.Phone?.Replace(" ", "", StringComparison.Ordinal);
        row.Notes = request.Notes;
        row.Type = request.Type;
        Live.PublishMemberChanged(new MemberChangedEvent(id, MemberChangeKind.Updated));
        return Task.FromResult(ToDto(row));
    }

    public Task<SubscriptionDto> RenewMemberAsync(Guid id, int planId, CancellationToken ct = default)
    {
        var row = Member(id);
        var subscription = AddMembership(row.Id, Plan(planId), DateTime.UtcNow);
        Live.PublishMemberChanged(new MemberChangedEvent(id, MemberChangeKind.Renewed));
        return Task.FromResult(subscription);
    }

    public Task<IReadOnlyList<SubscriptionDto>> GetMemberSubscriptionsAsync(Guid id, CancellationToken ct = default)
    {
        _ = Member(id);
        return ListOf(subscriptions.Where(s => s.MemberId == id).OrderByDescending(s => s.StartDate));
    }

    public Task<QrCodeResponse> ReissueQrCodeAsync(Guid id, CancellationToken ct = default)
    {
        var row = Member(id);
        row.QrCode = NewQrCode();
        Live.PublishMemberChanged(new MemberChangedEvent(id, MemberChangeKind.QrCodeReissued));
        return Task.FromResult(new QrCodeResponse(row.QrCode));
    }

    public Task UploadMemberPhotoAsync(Guid id, byte[] image, string contentType, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(image);
        var row = Member(id);
        photos[id] = image;
        row.HasPhoto = true;
        row.PhotoVersion++;
        Live.PublishMemberChanged(new MemberChangedEvent(id, MemberChangeKind.PhotoChanged));
        return Task.CompletedTask;
    }

    public Task<byte[]?> GetMemberPhotoAsync(Guid id, CancellationToken ct = default) => Task.FromResult(PhotoOf(Member(id)));

    public Task<byte[]> GetBytesAsync(string relativeUrl, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(relativeUrl);
        var path = relativeUrl.TrimStart('/').Split('?')[0];
        var parts = path.Split('/');
        if (parts is ["api", "members", var idText, "photo"] && Guid.TryParse(idText, out var id)
            && members.FirstOrDefault(m => m.Id == id) is { } row && PhotoOf(row) is { } photo)
        {
            return Task.FromResult(photo);
        }

        throw NotFound("not_found", $"Nothing at {relativeUrl}.");
    }

    // Check-in

    public Task<CheckInResultDto> CheckInAsync(CheckInRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var row = request.Code is { Length: > 0 } code
            ? members.FirstOrDefault(m => m.QrCode == code)
            : members.FirstOrDefault(m => m.MemberNo == request.MemberNo);
        var method = request.Code is { Length: > 0 } ? CheckInMethod.QrCode : CheckInMethod.MemberNo;
        var result = NewCheckIn(row, DateTime.UtcNow, request.Station, method);
        checkIns.Add(result);
        Live.PublishCheckIn(result);
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<CheckInResultDto>> GetCheckInsAsync(DateOnly? from = null, DateOnly? to = null, Guid? memberId = null, CancellationToken ct = default) =>
        ListOf(checkIns
            .Where(c => InRange(c.At, from, to) && (memberId is null || c.MemberId == memberId))
            .OrderByDescending(c => c.At));

    // Plans

    public Task<IReadOnlyList<PlanDto>> GetPlansAsync(bool includeInactive = false, CancellationToken ct = default) =>
        ListOf(plans.Where(p => includeInactive || p.IsActive).OrderBy(p => p.Months));

    public Task<PlanDto> CreatePlanAsync(SavePlanRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var plan = new PlanDto(plans.Count == 0 ? 1 : plans.Max(p => p.Id) + 1, request.Months, request.Price, request.IsActive);
        plans.Add(plan);
        return Task.FromResult(plan);
    }

    public Task<PlanDto> UpdatePlanAsync(int id, SavePlanRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var index = plans.FindIndex(p => p.Id == id);
        if (index < 0)
        {
            throw NotFound("plan_not_found", "No such plan.");
        }

        plans[index] = new PlanDto(id, request.Months, request.Price, request.IsActive);
        return Task.FromResult(plans[index]);
    }

    // Private trainers

    public Task<IReadOnlyList<PrivateTrainerDto>> GetPrivateTrainersAsync(CancellationToken ct = default) =>
        ListOf(members
            .Where(m => m.Type == MemberType.PrivateTrainer)
            .OrderBy(m => m.MemberNo)
            .Select(m => new PrivateTrainerDto(
                m.Id, m.MemberNo, MemberNumber.Format(m.MemberNo), m.FullName, m.Phone, PhotoUrl(m),
                members.Count(c => c.PrivateTrainerId == m.Id),
                StatusOf(m.Id, SubscriptionKind.Membership),
                StatusOf(m.Id, SubscriptionKind.PrivateTraining))));

    public Task<IReadOnlyList<MemberSummary>> GetPrivateTrainerClientsAsync(Guid trainerId, CancellationToken ct = default)
    {
        _ = Trainer(trainerId);
        return ListOf(members.Where(m => m.PrivateTrainerId == trainerId).OrderBy(m => m.MemberNo).Select(ToSummary));
    }

    public Task LinkClientAsync(Guid trainerId, Guid memberId, CancellationToken ct = default)
    {
        _ = Trainer(trainerId);
        if (trainerId == memberId)
        {
            throw Bad("cannot_link_self", "A trainer cannot be his own client.");
        }

        Member(memberId).PrivateTrainerId = trainerId;
        Live.PublishMemberChanged(new MemberChangedEvent(memberId, MemberChangeKind.TrainerLinkChanged));
        return Task.CompletedTask;
    }

    public Task UnlinkClientAsync(Guid trainerId, Guid memberId, CancellationToken ct = default)
    {
        var client = Member(memberId);
        if (client.PrivateTrainerId != trainerId)
        {
            throw NotFound("client_not_linked", "This member is not a client of this trainer.");
        }

        client.PrivateTrainerId = null;
        Live.PublishMemberChanged(new MemberChangedEvent(memberId, MemberChangeKind.TrainerLinkChanged));
        return Task.CompletedTask;
    }

    public Task<SubscriptionDto> AddPrivateServiceAsync(Guid trainerId, int months, CancellationToken ct = default)
    {
        _ = Trainer(trainerId);
        if (months < 1)
        {
            throw Bad("validation", "Months must be 1 or more.");
        }

        var latest = subscriptions.Where(s => s.MemberId == trainerId && s.Kind == SubscriptionKind.PrivateTraining).Select(s => (DateOnly?)s.EndDate).Max();
        var start = SubscriptionRules.GetRenewalStartDate(latest, Today);
        var subscription = AddSubscription(trainerId, SubscriptionKind.PrivateTraining, null, months, start, months * settings.PrivateTrainingMonthlyFee, DateTime.UtcNow);
        Live.PublishMemberChanged(new MemberChangedEvent(trainerId, MemberChangeKind.PrivateServiceChanged));
        return Task.FromResult(subscription);
    }

    // Staff and ratings

    public Task<IReadOnlyList<StaffDto>> GetStaffAsync(StaffType? type = null, bool includeInactive = true, CancellationToken ct = default) =>
        ListOf(staff.Where(s => (type is null || s.Type == type) && (includeInactive || s.IsActive)).OrderBy(s => s.Id));

    public Task<StaffDto> GetStaffMemberAsync(int id, CancellationToken ct = default) => Task.FromResult(Staff(id));

    public Task<StaffDto> CreateStaffAsync(CreateStaffRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CheckUsername(request.Username, null);
        var hasLogin = !string.IsNullOrWhiteSpace(request.Username);
        var created = new StaffDto(
            NextId(), request.FullName, request.Phone, request.Type, true, hasLogin ? request.Username : null,
            hasLogin ? request.Role : null, hasLogin, DateTime.UtcNow, null);
        staff.Add(created);
        if (hasLogin && request.Password is { } password)
        {
            passwords[created.Id] = password;
        }

        return Task.FromResult(created);
    }

    public Task<StaffDto> UpdateStaffAsync(int id, UpdateStaffRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var old = Staff(id);
        CheckUsername(request.Username, id);
        var hasLogin = !string.IsNullOrWhiteSpace(request.Username);
        var updated = old with
        {
            FullName = request.FullName,
            Type = request.Type,
            IsActive = request.IsActive,
            Phone = request.Phone,
            Username = hasLogin ? request.Username : null,
            Role = hasLogin ? request.Role : null,
            CanLogIn = hasLogin,
        };
        staff[staff.IndexOf(old)] = updated;
        return Task.FromResult(updated);
    }

    public Task ResetStaffPasswordAsync(int id, string newPassword, CancellationToken ct = default)
    {
        _ = Staff(id);
        passwords[id] = newPassword;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RatingDto>> GetRatingsAsync(int staffId, CancellationToken ct = default)
    {
        _ = Staff(staffId);
        return ListOf(ratings.Where(r => r.StaffId == staffId).OrderByDescending(r => r.Year).ThenByDescending(r => r.Month));
    }

    public Task<RatingDto> SetRatingAsync(int staffId, int year, int month, SetRatingRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var trainer = Staff(staffId);
        if (trainer.Type != StaffType.ClubTrainer)
        {
            throw Bad("not_a_club_trainer", "Only club trainers are rated.");
        }

        if (request.Stars is < 1 or > 5)
        {
            throw Bad("invalid_stars", "Stars must be 1 to 5.");
        }

        ratings.RemoveAll(r => r.StaffId == staffId && r.Year == year && r.Month == month);
        var rating = new RatingDto(NextId(), staffId, trainer.FullName, year, month, request.Stars, request.Note, current?.FullName ?? "Admin", DateTime.UtcNow);
        ratings.Add(rating);
        return Task.FromResult(rating);
    }

    public Task<IReadOnlyList<RatingDto>> GetMonthRatingsAsync(int year, int month, CancellationToken ct = default) =>
        ListOf(ratings.Where(r => r.Year == year && r.Month == month).OrderBy(r => r.StaffName, StringComparer.Ordinal));

    // Cafeteria: categories and products

    public Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync(bool includeInactive = false, CancellationToken ct = default) =>
        ListOf(categories.Where(c => includeInactive || c.IsActive).OrderBy(c => c.SortOrder).ThenBy(c => c.Id));

    public Task<CategoryDto> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var category = new CategoryDto(categories.Max(c => c.Id) + 1, request.NameEn, request.NameAr, request.SortOrder, request.IsActive);
        categories.Add(category);
        return Task.FromResult(category);
    }

    public Task<CategoryDto> UpdateCategoryAsync(int id, SaveCategoryRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var index = categories.FindIndex(c => c.Id == id);
        if (index < 0)
        {
            throw NotFound("category_not_found", "No such category.");
        }

        categories[index] = new CategoryDto(id, request.NameEn, request.NameAr, request.SortOrder, request.IsActive);
        return Task.FromResult(categories[index]);
    }

    public Task<IReadOnlyList<ProductDto>> GetProductsAsync(string? query = null, int? categoryId = null, bool includeInactive = false, CancellationToken ct = default)
    {
        var q = SearchText.Normalize(query);
        return ListOf(products
            .Where(p => (includeInactive || p.IsActive) && (categoryId is null || p.CategoryId == categoryId))
            .Where(p => q.Length == 0 || SearchText.Normalize(p.NameEn).Contains(q, StringComparison.Ordinal)
                        || SearchText.Normalize(p.NameAr).Contains(q, StringComparison.Ordinal) || p.Barcode == query?.Trim())
            .OrderBy(p => Category(p.CategoryId).SortOrder).ThenBy(p => p.NameEn, StringComparer.OrdinalIgnoreCase)
            .Select(ToDto));
    }

    public Task<ProductDto> GetProductAsync(int id, CancellationToken ct = default) => Task.FromResult(ToDto(Product(id)));

    public Task<ProductDto?> FindProductByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        var row = products.FirstOrDefault(p => p.Barcode is not null && p.Barcode == barcode?.Trim());
        return Task.FromResult(row is null ? null : ToDto(row));
    }

    public Task<ProductDto> CreateProductAsync(SaveProductRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CheckBarcode(request.Barcode, null);
        _ = Category(request.CategoryId);
        var row = new ProductRow
        {
            Id = products.Max(p => p.Id) + 1,
            NameEn = request.NameEn,
            NameAr = request.NameAr,
            CategoryId = request.CategoryId,
            Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim(),
            Price = request.Price,
            MinStock = request.MinStock,
            IsActive = request.IsActive,
        };
        products.Add(row);
        Live.PublishProductChanged(new ProductChangedEvent(row.Id));
        return Task.FromResult(ToDto(row));
    }

    public Task<ProductDto> UpdateProductAsync(int id, SaveProductRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var row = Product(id);
        CheckBarcode(request.Barcode, id);
        _ = Category(request.CategoryId);
        row.NameEn = request.NameEn;
        row.NameAr = request.NameAr;
        row.CategoryId = request.CategoryId;
        row.Barcode = string.IsNullOrWhiteSpace(request.Barcode) ? null : request.Barcode.Trim();
        row.Price = request.Price;
        row.MinStock = request.MinStock;
        row.IsActive = request.IsActive;
        Live.PublishProductChanged(new ProductChangedEvent(id));
        return Task.FromResult(ToDto(row));
    }

    // Cafeteria: stock

    public Task<IReadOnlyList<StockLevelDto>> GetStockAsync(bool lowOnly = false, CancellationToken ct = default) =>
        ListOf(products.Where(p => p.IsActive).Select(ToLevel).Where(l => !lowOnly || l.IsLow).OrderBy(l => l.NameEn, StringComparer.OrdinalIgnoreCase));

    public Task<StockLevelDto> StockInAsync(StockInRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Quantity < 1)
        {
            throw Bad("invalid_quantity", "The quantity must be 1 or more.");
        }

        var row = Product(request.ProductId);
        AddMovement(row, request.Quantity, StockMovementType.StockIn, null, request.Note, DateTime.UtcNow);
        var level = ToLevel(row);
        Live.PublishStockChanged(new StockChangedEvent([level]));
        return Task.FromResult(level);
    }

    public Task<StockLevelDto> AdjustStockAsync(StockAdjustmentRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var row = Product(request.ProductId);
        var stock = StockOf(row.Id);
        var delta = request.CountedQuantity is { } counted ? counted - stock : request.Delta ?? 0;
        if (delta == 0 && request.CountedQuantity is null)
        {
            throw Bad("invalid_adjustment", "Give a change or a counted quantity.");
        }

        if (stock + delta < 0)
        {
            throw Bad("insufficient_stock", "The stock cannot go below zero.");
        }

        if (delta != 0)
        {
            AddMovement(row, delta, StockMovementType.Adjustment, null, request.Note, DateTime.UtcNow);
        }

        var level = ToLevel(row);
        Live.PublishStockChanged(new StockChangedEvent([level]));
        return Task.FromResult(level);
    }

    public Task<IReadOnlyList<StockMovementDto>> GetStockMovementsAsync(int? productId = null, DateOnly? from = null, DateOnly? to = null, CancellationToken ct = default) =>
        ListOf(movements.Where(m => (productId is null || m.ProductId == productId) && InRange(m.At, from, to)).OrderByDescending(m => m.At).ThenByDescending(m => m.Id));

    // Cafeteria: sales and shifts

    public Task<ReceiptDto> CreateSaleAsync(CreateSaleRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var cashier = current ?? throw new GymApiException(HttpStatusCode.Unauthorized, null, "Not logged in.");
        if (request.Lines.Count == 0)
        {
            throw Bad("empty_sale", "The sale has no items.");
        }

        var lines = request.Lines.GroupBy(l => l.ProductId).Select(g => (Product: Product(g.Key), Quantity: g.Sum(l => l.Quantity))).ToList();
        var shortages = lines.Where(l => l.Quantity > StockOf(l.Product.Id)).Select(l => l.Product.NameEn).ToList();
        if (shortages.Count > 0)
        {
            throw Bad("insufficient_stock", "Not enough stock: " + string.Join(", ", shortages));
        }

        var total = lines.Sum(l => l.Product.Price * l.Quantity);
        if (request.CashReceived is { } cash && cash < total)
        {
            throw Bad("cash_received_too_low", "The cash received is less than the total.");
        }

        var shift = shifts.FirstOrDefault(s => s.CashierId == cashier.Id && s.ClosedAt is null)
                    ?? OpenShift(cashier, request.Station, 0, DateTime.UtcNow);
        var receipt = AddSale(cashier, shift, request.Station, DateTime.UtcNow, lines.Select(l => (l.Product, l.Quantity)).ToList(), request.CashReceived);
        Live.PublishStockChanged(new StockChangedEvent(lines.Select(l => ToLevel(l.Product)).ToList()));
        Live.PublishSaleCompleted(new SaleCompletedEvent(receipt.Id, receipt.Number, receipt.Station, receipt.Total, receipt.At));
        return Task.FromResult(receipt);
    }

    public Task<ReceiptDto> GetSaleAsync(long id, CancellationToken ct = default) =>
        Task.FromResult(sales.FirstOrDefault(s => s.Id == id) ?? throw NotFound("sale_not_found", "No such sale."));

    public Task<IReadOnlyList<SaleSummaryDto>> GetSalesAsync(DateOnly? from = null, DateOnly? to = null, int? cashierId = null, CancellationToken ct = default) =>
        ListOf(sales
            .Where(s => InRange(s.At, from, to) && (cashierId is null || s.CashierId == cashierId))
            .OrderByDescending(s => s.At)
            .Select(s => new SaleSummaryDto(s.Id, s.Number, s.At, s.Station, s.CashierId, s.CashierName, s.Total, s.Lines.Sum(l => l.Quantity))));

    public Task<ShiftDto> OpenShiftAsync(OpenShiftRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var cashier = current ?? throw new GymApiException(HttpStatusCode.Unauthorized, null, "Not logged in.");
        if (shifts.Any(s => s.CashierId == cashier.Id && s.ClosedAt is null))
        {
            throw new GymApiException(HttpStatusCode.Conflict, "shift_already_open", "A shift is already open.");
        }

        return Task.FromResult(OpenShift(cashier, request.Station, request.OpeningCash, DateTime.UtcNow));
    }

    public Task<ShiftDto?> GetCurrentShiftAsync(CancellationToken ct = default) =>
        Task.FromResult(current is null ? null : shifts.FirstOrDefault(s => s.CashierId == current.Id && s.ClosedAt is null));

    public Task<ShiftSummaryDto> CloseShiftAsync(int id, CloseShiftRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var index = shifts.FindIndex(s => s.Id == id);
        if (index < 0)
        {
            throw NotFound("shift_not_found", "No such shift.");
        }

        if (shifts[index].ClosedAt is not null)
        {
            throw new GymApiException(HttpStatusCode.Conflict, "shift_closed", "The shift is already closed.");
        }

        shifts[index] = shifts[index] with { ClosedAt = DateTime.UtcNow, ClosingCash = request.ClosingCash, Note = request.Note };
        return Task.FromResult(Summary(shifts[index]));
    }

    public Task<ShiftSummaryDto> GetShiftSummaryAsync(int id, CancellationToken ct = default) =>
        Task.FromResult(Summary(shifts.FirstOrDefault(s => s.Id == id) ?? throw NotFound("shift_not_found", "No such shift.")));

    public Task<IReadOnlyList<ShiftDto>> GetShiftsAsync(DateOnly? from = null, DateOnly? to = null, int? cashierId = null, CancellationToken ct = default) =>
        ListOf(shifts.Where(s => InRange(s.OpenedAt, from, to) && (cashierId is null || s.CashierId == cashierId)).OrderByDescending(s => s.OpenedAt));

    // Admin

    public Task<DashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var today = checkIns.Where(c => LocalDay(c.At) == Today).ToList();
        var admitted = today.Where(c => c.Admitted).ToList();
        var perHour = admitted.GroupBy(c => c.At.ToLocalTime().Hour).ToDictionary(g => g.Key, g => g.Count());
        var statuses = members.Select(m => StatusOf(m.Id, SubscriptionKind.Membership)).ToList();
        var paidToday = subscriptions.Where(s => LocalDay(s.CreatedAt) == Today).ToList();
        var revenueSubscriptions = paidToday.Where(s => s.Kind == SubscriptionKind.Membership).Sum(s => s.PricePaid);
        var revenuePrivate = paidToday.Where(s => s.Kind == SubscriptionKind.PrivateTraining).Sum(s => s.PricePaid);
        var revenueCafeteria = sales.Where(s => LocalDay(s.At) == Today).Sum(s => s.Total);
        var expiring = members
            .Select(m => (Member: m, Status: StatusOf(m.Id, SubscriptionKind.Membership)))
            .Where(x => x.Status?.Status == SubscriptionStatus.ExpiringSoon)
            .OrderBy(x => x.Status!.EndDate).ThenBy(x => x.Member.MemberNo)
            .Select(x => ToSummary(x.Member))
            .ToList();

        return Task.FromResult(new DashboardDto(
            Today,
            admitted.Count,
            today.Count - admitted.Count,
            Enumerable.Range(0, 24).Select(h => new HourCount(h, perHour.GetValueOrDefault(h))).ToList(),
            statuses.Count(s => s?.Status == SubscriptionStatus.Active),
            statuses.Count(s => s?.Status == SubscriptionStatus.ExpiringSoon),
            statuses.Count(s => s?.Status == SubscriptionStatus.Expired),
            revenueSubscriptions,
            revenuePrivate,
            revenueCafeteria,
            revenueSubscriptions + revenuePrivate + revenueCafeteria,
            products.Where(p => p.IsActive).Select(ToLevel).Where(l => l.IsLow).OrderBy(l => l.NameEn, StringComparer.OrdinalIgnoreCase).ToList(),
            expiring));
    }

    public Task<SubscriptionReportDto> GetSubscriptionReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var rows = subscriptions.Where(s => InRange(s.CreatedAt, from, to)).ToList();
        return Task.FromResult(new SubscriptionReportDto(
            from,
            to,
            rows.Count,
            rows.Sum(s => s.PricePaid),
            rows.GroupBy(s => (s.Kind, s.Months)).OrderBy(g => g.Key.Kind).ThenBy(g => g.Key.Months)
                .Select(g => new SubscriptionReportRow(g.Key.Kind, g.Key.Months, g.Count(), g.Sum(s => s.PricePaid))).ToList(),
            Days(from, to).Select(d => rows.Where(s => LocalDay(s.CreatedAt) == d).ToList())
                .Select((list, i) => new DayAmount(from.AddDays(i), list.Count, list.Sum(s => s.PricePaid))).ToList(),
            rows.GroupBy(s => (s.CreatedById, s.CreatedBy)).OrderByDescending(g => g.Sum(s => s.PricePaid))
                .Select(g => new StaffAmount(g.Key.CreatedById, g.Key.CreatedBy, g.Count(), g.Sum(s => s.PricePaid))).ToList()));
    }

    public Task<AttendanceReportDto> GetAttendanceReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var rows = checkIns.Where(c => InRange(c.At, from, to)).ToList();
        var admitted = rows.Where(c => c.Admitted).ToList();
        var perHour = admitted.GroupBy(c => c.At.ToLocalTime().Hour).ToDictionary(g => g.Key, g => g.Count());
        return Task.FromResult(new AttendanceReportDto(
            from,
            to,
            rows.Count,
            admitted.Count,
            rows.Count - admitted.Count,
            admitted.Where(c => c.MemberId is not null).Select(c => c.MemberId).Distinct().Count(),
            Days(from, to).Select(d => new DayCount(d, admitted.Count(c => LocalDay(c.At) == d))).ToList(),
            Enumerable.Range(0, 24).Select(h => new HourCount(h, perHour.GetValueOrDefault(h))).ToList()));
    }

    public Task<SalesReportDto> GetSalesReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        var rows = sales.Where(s => InRange(s.At, from, to)).ToList();
        return Task.FromResult(new SalesReportDto(
            from,
            to,
            rows.Count,
            rows.Sum(s => s.Total),
            Days(from, to).Select(d => rows.Where(s => LocalDay(s.At) == d).ToList())
                .Select((list, i) => new DayAmount(from.AddDays(i), list.Count, list.Sum(s => s.Total))).ToList(),
            ProductTotals(rows),
            rows.GroupBy(s => (s.CashierId, s.CashierName)).OrderByDescending(g => g.Sum(s => s.Total))
                .Select(g => new StaffAmount(g.Key.CashierId, g.Key.CashierName, g.Count(), g.Sum(s => s.Total))).ToList()));
    }

    public Task<SettingsDto> GetSettingsAsync(CancellationToken ct = default) => Task.FromResult(settings);

    public Task<SettingsDto> UpdateSettingsAsync(SettingsDto settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (settings.ExpiringSoonDays < 0 || settings.PrivateTrainingMonthlyFee < 0)
        {
            throw Bad("invalid_setting", "Settings cannot be negative.");
        }

        this.settings = settings;
        Live.PublishSettingsChanged(settings);
        return Task.FromResult(settings);
    }

    // Helpers

    private LoginResponse LogIn(StaffDto user)
    {
        var index = staff.IndexOf(user);
        user = user with { LastLoginAt = DateTime.UtcNow };
        staff[index] = user;
        current = user;
        AccessToken = "sample-token-" + user.Id.ToString(CultureInfo.InvariantCulture);
        return new LoginResponse(AccessToken, DateTime.UtcNow.AddHours(12), user);
    }

    private int NextId() => ++nextId;

    private static Task<IReadOnlyList<T>> ListOf<T>(IEnumerable<T> items) => Task.FromResult<IReadOnlyList<T>>(items.ToList());

    private static GymApiException Bad(string code, string message) => new(HttpStatusCode.BadRequest, code, message);

    private static GymApiException NotFound(string code, string message) => new(HttpStatusCode.NotFound, code, message);

    private static DateOnly LocalDay(DateTime utc) => DateOnly.FromDateTime(utc.ToLocalTime());

    private static bool InRange(DateTime utc, DateOnly? from, DateOnly? to)
    {
        var day = LocalDay(utc);
        return (from is null || day >= from) && (to is null || day <= to);
    }

    private static IEnumerable<DateOnly> Days(DateOnly from, DateOnly to)
    {
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            yield return d;
        }
    }

    private static string NewQrCode()
    {
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var chars = new char[24];
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = alphabet[Random.Shared.Next(alphabet.Length)];
        }

        return "TF1-" + new string(chars);
    }

    private MemberRow Member(Guid id) => members.FirstOrDefault(m => m.Id == id) ?? throw NotFound("member_not_found", "No such member.");

    private MemberRow Trainer(Guid id)
    {
        var trainer = Member(id);
        return trainer.Type == MemberType.PrivateTrainer ? trainer : throw Bad("not_a_private_trainer", "This member is not a private trainer.");
    }

    private StaffDto Staff(int id) => staff.FirstOrDefault(s => s.Id == id) ?? throw NotFound("staff_not_found", "No such staff member.");

    private PlanDto Plan(int id) => plans.FirstOrDefault(p => p.Id == id) ?? throw NotFound("plan_not_found", "No such plan.");

    private ProductRow Product(int id) => products.FirstOrDefault(p => p.Id == id) ?? throw NotFound("product_not_found", "No such product.");

    private CategoryDto Category(int id) => categories.FirstOrDefault(c => c.Id == id) ?? throw NotFound("category_not_found", "No such category.");

    private void CheckNationalNo(string? nationalNo, Guid? self)
    {
        if (!string.IsNullOrWhiteSpace(nationalNo) && members.Any(m => m.NationalNo == nationalNo && m.Id != self))
        {
            throw new GymApiException(HttpStatusCode.Conflict, "duplicate_national_no", "A member with this national number exists.");
        }
    }

    private void CheckUsername(string? username, int? self)
    {
        if (!string.IsNullOrWhiteSpace(username)
            && staff.Any(s => string.Equals(s.Username, username, StringComparison.OrdinalIgnoreCase) && s.Id != self))
        {
            throw new GymApiException(HttpStatusCode.Conflict, "duplicate_username", "This username is taken.");
        }
    }

    private void CheckBarcode(string? barcode, int? self)
    {
        if (!string.IsNullOrWhiteSpace(barcode) && products.Any(p => p.Barcode == barcode.Trim() && p.Id != self))
        {
            throw new GymApiException(HttpStatusCode.Conflict, "duplicate_barcode", "Another product has this barcode.");
        }
    }

    private bool Matches(MemberRow m, string query)
    {
        var q = SearchText.Normalize(query);
        var digits = SearchText.NormalizeDigits(query.Trim()).Replace(" ", "", StringComparison.Ordinal);
        return SearchText.Normalize(m.FullName).Contains(q, StringComparison.Ordinal)
               || (digits.Length >= 3 && m.Phone?.Contains(digits, StringComparison.Ordinal) == true)
               || (digits.Length >= 3 && m.NationalNo?.Contains(digits, StringComparison.Ordinal) == true)
               || (MemberNumber.TryParse(query, out var no) && m.MemberNo == no)
               || m.QrCode == query.Trim();
    }

    private SubscriptionStatusDto? StatusOf(Guid memberId, SubscriptionKind kind)
    {
        var end = subscriptions.Where(s => s.MemberId == memberId && s.Kind == kind).Select(s => (DateOnly?)s.EndDate).Max();
        if (end is not { } endDate)
        {
            return null;
        }

        var state = SubscriptionRules.GetStatus(endDate, Today, settings.ExpiringSoonDays);
        return new SubscriptionStatusDto(state.Status, endDate, state.DaysLeft, state.DaysOverdue);
    }

    private byte[]? PhotoOf(MemberRow row)
    {
        if (!row.HasPhoto)
        {
            return null;
        }

        if (!photos.TryGetValue(row.Id, out var photo))
        {
            photo = PlaceholderPhoto.Create(row.MemberNo);
            photos[row.Id] = photo;
        }

        return photo;
    }

    private static string? PhotoUrl(MemberRow m) =>
        m.HasPhoto ? FormattableString.Invariant($"api/members/{m.Id}/photo?v={m.PhotoVersion}") : null;

    private MemberSummary ToSummary(MemberRow m) => new(
        m.Id, m.MemberNo, MemberNumber.Format(m.MemberNo), m.Type, m.FullName, m.Phone, m.NationalNo, PhotoUrl(m), m.PrivateTrainerId,
        StatusOf(m.Id, SubscriptionKind.Membership));

    private MemberDto ToDto(MemberRow m) => new(
        m.Id,
        m.MemberNo,
        MemberNumber.Format(m.MemberNo),
        m.Type,
        m.FirstName,
        m.FatherName,
        m.LastName,
        m.MotherName,
        m.FullName,
        m.BirthDate,
        m.BirthPlace,
        m.NationalNo,
        m.Phone,
        m.Notes,
        PhotoUrl(m),
        m.QrCode,
        m.PrivateTrainerId,
        m.PrivateTrainerId is { } trainerId ? members.FirstOrDefault(t => t.Id == trainerId)?.FullName : null,
        StatusOf(m.Id, SubscriptionKind.Membership),
        StatusOf(m.Id, SubscriptionKind.PrivateTraining),
        m.CreatedAt);

    private int StockOf(int productId) => movements.Where(m => m.ProductId == productId).Sum(m => m.Quantity);

    private ProductDto ToDto(ProductRow p)
    {
        var category = Category(p.CategoryId);
        var stock = StockOf(p.Id);
        return new ProductDto(p.Id, p.NameEn, p.NameAr, p.CategoryId, category.NameEn, category.NameAr, p.Barcode, p.Price, p.MinStock, p.IsActive, stock, stock < p.MinStock);
    }

    private StockLevelDto ToLevel(ProductRow p)
    {
        var stock = StockOf(p.Id);
        return new StockLevelDto(p.Id, p.NameEn, p.NameAr, stock, p.MinStock, stock < p.MinStock);
    }

    private CheckInResultDto NewCheckIn(MemberRow? row, DateTime at, string station, CheckInMethod method)
    {
        if (row is null)
        {
            return new CheckInResultDto(NextId(), CheckInResult.UnknownMember, false, at, station, method, null, null, null, null, null, null, null, 0, 0, null);
        }

        var status = StatusOf(row.Id, SubscriptionKind.Membership);
        var result = status?.Status switch
        {
            SubscriptionStatus.Active => CheckInResult.Active,
            SubscriptionStatus.ExpiringSoon => CheckInResult.ExpiringSoon,
            SubscriptionStatus.Expired => CheckInResult.Expired,
            _ => CheckInResult.NoSubscription,
        };
        return new CheckInResultDto(
            NextId(), result, result is CheckInResult.Active or CheckInResult.ExpiringSoon, at, station, method,
            row.Id, row.MemberNo, MemberNumber.Format(row.MemberNo), row.FullName, row.Type, PhotoUrl(row),
            status?.EndDate, status?.DaysLeft ?? 0, status?.DaysOverdue ?? 0,
            row.Type == MemberType.PrivateTrainer ? StatusOf(row.Id, SubscriptionKind.PrivateTraining) : null);
    }

    private SubscriptionDto AddMembership(Guid memberId, PlanDto plan, DateTime createdAt)
    {
        var latest = subscriptions.Where(s => s.MemberId == memberId && s.Kind == SubscriptionKind.Membership).Select(s => (DateOnly?)s.EndDate).Max();
        var start = SubscriptionRules.GetRenewalStartDate(latest, Today);
        return AddSubscription(memberId, SubscriptionKind.Membership, plan.Id, plan.Months, start, plan.Price, createdAt);
    }

    private SubscriptionDto AddSubscription(Guid memberId, SubscriptionKind kind, int? planId, int months, DateOnly start, long price, DateTime createdAt)
    {
        var by = current ?? staff.First(s => s.Role == StaffRole.Reception);
        var subscription = new SubscriptionDto(
            NextId(), memberId, kind, planId, months, start, SubscriptionRules.CalculateEndDate(start, months), price, by.Id, by.FullName, createdAt);
        subscriptions.Add(subscription);
        return subscription;
    }

    private void AddMovement(ProductRow product, int quantity, StockMovementType type, long? saleId, string? note, DateTime at, StaffDto? by = null)
    {
        by ??= current ?? staff.First(s => s.Role == StaffRole.Cashier);
        movements.Add(new StockMovementDto(NextId(), product.Id, product.NameEn, product.NameAr, quantity, type, saleId, note, by.FullName, at));
    }

    private ShiftDto OpenShift(StaffDto cashier, string station, long openingCash, DateTime at)
    {
        var shift = new ShiftDto(shifts.Count == 0 ? 1 : shifts.Max(s => s.Id) + 1, cashier.Id, cashier.FullName, station, at, null, openingCash, null, null);
        shifts.Add(shift);
        return shift;
    }

    private ReceiptDto AddSale(StaffDto cashier, ShiftDto shift, string station, DateTime at, List<(ProductRow Product, int Quantity)> lines, long? cashReceived)
    {
        var id = sales.Count == 0 ? 1 : sales.Max(s => s.Id) + 1;
        var number = sales.Count == 0 ? 1001 : sales.Max(s => s.Number) + 1;
        var receiptLines = lines.Select(l => new ReceiptLineDto(l.Product.Id, l.Product.NameEn, l.Product.NameAr, l.Quantity, l.Product.Price, l.Product.Price * l.Quantity)).ToList();
        var total = receiptLines.Sum(l => l.LineTotal);
        var receipt = new ReceiptDto(id, number, at, station, cashier.Id, cashier.FullName, shift.Id, receiptLines, total, cashReceived, cashReceived - total);
        sales.Add(receipt);
        foreach (var (product, quantity) in lines)
        {
            AddMovement(product, -quantity, StockMovementType.Sale, id, null, at, cashier);
        }

        return receipt;
    }

    private ShiftSummaryDto Summary(ShiftDto shift)
    {
        var shiftSales = sales.Where(s => s.ShiftId == shift.Id).ToList();
        var total = shiftSales.Sum(s => s.Total);
        var expected = shift.OpeningCash + total;
        return new ShiftSummaryDto(shift, shiftSales.Count, total, expected, shift.ClosingCash - expected, ProductTotals(shiftSales));
    }

    private static List<ProductSalesDto> ProductTotals(IEnumerable<ReceiptDto> receipts) =>
        receipts.SelectMany(s => s.Lines)
            .GroupBy(l => (l.ProductId, l.NameEn, l.NameAr))
            .Select(g => new ProductSalesDto(g.Key.ProductId, g.Key.NameEn, g.Key.NameAr, g.Sum(l => l.Quantity), g.Sum(l => l.LineTotal)))
            .OrderByDescending(p => p.Total)
            .ToList();

    private sealed class MemberRow
    {
        public Guid Id { get; init; }

        public int MemberNo { get; init; }

        public MemberType Type { get; set; }

        public string FirstName { get; set; } = "";

        public string? FatherName { get; set; }

        public string LastName { get; set; } = "";

        public string? MotherName { get; set; }

        public string FullName => FatherName is { Length: > 0 } ? $"{FirstName} {FatherName} {LastName}" : $"{FirstName} {LastName}";

        public DateOnly? BirthDate { get; set; }

        public string? BirthPlace { get; set; }

        public string? NationalNo { get; set; }

        public string? Phone { get; set; }

        public string? Notes { get; set; }

        public bool HasPhoto { get; set; }

        public int PhotoVersion { get; set; } = 1;

        public string QrCode { get; set; } = "";

        public Guid? PrivateTrainerId { get; set; }

        public DateTime CreatedAt { get; init; }
    }

    private sealed class ProductRow
    {
        public int Id { get; init; }

        public string NameEn { get; set; } = "";

        public string NameAr { get; set; } = "";

        public int CategoryId { get; set; }

        public string? Barcode { get; set; }

        public long Price { get; set; }

        public int MinStock { get; set; }

        public bool IsActive { get; set; } = true;
    }
}
