namespace Gym.Infrastructure.Services;

/// <summary>Members, their subscriptions, QR codes, photos and private trainer links.</summary>
public sealed class MemberService(GymDbContext db, GymClock clock, SettingsService settings)
{
    public const int MaxPrivateServiceMonths = 36;

    public async Task<MemberDto> CreateAsync(CreateMemberRequest request, int staffId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var now = clock.UtcNow;
        var member = new Member
        {
            Id = Guid.CreateVersion7(),
            Type = Guard.Defined(request.Type, "Member type"),
            QrCode = MemberQrCode.Generate(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        ApplyDetails(member, request.FirstName, request.LastName, request.FatherName, request.MotherName,
            request.BirthDate, request.BirthPlace, request.NationalNo, request.Phone, request.Notes);
        await EnsureNationalNoFreeAsync(member.NationalNo, member.Id, ct);

        if (request.PrivateTrainerId is { } trainerId)
        {
            member.LinkToPrivateTrainer(await FindAsync(trainerId, ct));
        }

        db.Members.Add(member);
        if (request.PlanId is { } planId)
        {
            var plan = await FindActivePlanAsync(planId, ct);
            db.Subscriptions.Add(Subscription.Start(
                member.Id, SubscriptionKind.Membership, plan.Months, plan.Price, null, clock.Today, staffId, now, plan.Id));
        }

        await db.SaveChangesOrConflictAsync(ct);
        return await GetAsync(member.Id, ct);
    }

    public async Task<MemberDto> UpdateAsync(Guid id, UpdateMemberRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var member = await FindAsync(id, ct);
        ApplyDetails(member, request.FirstName, request.LastName, request.FatherName, request.MotherName,
            request.BirthDate, request.BirthPlace, request.NationalNo, request.Phone, request.Notes);
        var type = Guard.Defined(request.Type, "Member type");
        if (type != member.Type)
        {
            member.ChangeType(type, await db.Members.CountAsync(m => m.PrivateTrainerId == id, ct));
        }

        await EnsureNationalNoFreeAsync(member.NationalNo, id, ct);
        member.UpdatedAt = clock.UtcNow;
        await db.SaveChangesOrConflictAsync(ct);
        return await GetAsync(id, ct);
    }

    public async Task<MemberDto> GetAsync(Guid id, CancellationToken ct = default)
    {
        var row = await db.Members.AsNoTracking()
            .Where(m => m.Id == id)
            .Select(m => new
            {
                Member = m,
                Trainer = m.PrivateTrainer,
                MembershipEnd = m.Subscriptions.Where(s => s.Kind == SubscriptionKind.Membership).Max(s => (DateOnly?)s.EndDate),
                ServiceEnd = m.Subscriptions.Where(s => s.Kind == SubscriptionKind.PrivateTraining).Max(s => (DateOnly?)s.EndDate),
            })
            .FirstOrDefaultAsync(ct) ?? throw NotFound(id);

        var today = clock.Today;
        var threshold = await settings.GetExpiringSoonDaysAsync(ct);
        var m = row.Member;
        return new MemberDto(
            m.Id, m.MemberNo, MemberNumber.Format(m.MemberNo), m.Type,
            m.FirstName, m.FatherName, m.LastName, m.MotherName, m.FullName,
            m.BirthDate, m.BirthPlace, m.NationalNo, m.Phone, m.Notes,
            Dto.PhotoUrl(m.Id, m.PhotoFile), m.QrCode,
            m.PrivateTrainerId, row.Trainer?.FullName,
            Dto.Status(row.MembershipEnd, today, threshold),
            Dto.Status(row.ServiceEnd, today, threshold),
            m.CreatedAt);
    }

    public async Task<PagedResult<MemberSummary>> SearchAsync(
        string? text, SubscriptionStatus? status, MemberType? type, int page, int pageSize, CancellationToken ct = default)
    {
        (page, pageSize) = Guard.Paging(page, pageSize);
        var today = clock.Today;
        var threshold = await settings.GetExpiringSoonDaysAsync(ct);

        var query = db.Members.AsNoTracking().WhereMatches(text);
        if (type is { } memberType)
        {
            query = query.Where(m => m.Type == memberType);
        }

        if (status is { } wanted)
        {
            query = query.WhereMembershipStatus(wanted, today, threshold);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(m => m.MemberNo)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToSummariesAsync(today, threshold, ct);
        return new PagedResult<MemberSummary>(items, total, page, pageSize);
    }

    public async Task<SubscriptionDto> RenewAsync(Guid memberId, int planId, int staffId, CancellationToken ct = default)
    {
        var plan = await FindActivePlanAsync(planId, ct);
        return await AddSubscriptionAsync(memberId, SubscriptionKind.Membership, plan.Months, plan.Price, plan.Id, staffId, check: null, ct);
    }

    /// <summary>Buys or renews a private trainer's private training service (months × the fixed monthly fee).</summary>
    public async Task<SubscriptionDto> AddPrivateServiceAsync(Guid trainerId, int months, int staffId, CancellationToken ct = default)
    {
        Guard.Range(months, "Months", 1, MaxPrivateServiceMonths);
        var fee = (await settings.GetAsync(ct)).PrivateTrainingMonthlyFee;
        var price = PrivateTrainingRules.CalculatePrice(months, fee);
        return await AddSubscriptionAsync(
            trainerId, SubscriptionKind.PrivateTraining, months, price, null, staffId, PrivateTrainingRules.EnsureCanHaveService, ct);
    }

    public async Task<IReadOnlyList<SubscriptionDto>> GetSubscriptionsAsync(Guid memberId, CancellationToken ct = default)
    {
        await EnsureExistsAsync(memberId, ct);
        return await ProjectSubscriptions(db.Subscriptions.AsNoTracking()
                .Where(s => s.MemberId == memberId)
                .OrderByDescending(s => s.StartDate).ThenByDescending(s => s.Id))
            .ToListAsync(ct);
    }

    public async Task<QrCodeResponse> ReissueQrCodeAsync(Guid id, CancellationToken ct = default)
    {
        var member = await FindAsync(id, ct);
        member.ReissueQrCode();
        member.UpdatedAt = clock.UtcNow;
        await db.SaveChangesOrConflictAsync(ct);
        return new QrCodeResponse(member.QrCode);
    }

    public async Task EnsureExistsAsync(Guid id, CancellationToken ct = default)
    {
        if (!await db.Members.AnyAsync(m => m.Id == id, ct))
        {
            throw NotFound(id);
        }
    }

    /// <summary>Stores the new photo file name and returns the old one (to delete), or null.</summary>
    public async Task<string?> SetPhotoAsync(Guid id, string photoFile, CancellationToken ct = default)
    {
        var member = await FindAsync(id, ct);
        var old = member.PhotoFile;
        member.PhotoFile = photoFile;
        member.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        return old;
    }

    public async Task<string?> GetPhotoFileAsync(Guid id, CancellationToken ct = default) =>
        await db.Members.AsNoTracking().Where(m => m.Id == id).Select(m => m.PhotoFile).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<PrivateTrainerDto>> ListPrivateTrainersAsync(CancellationToken ct = default)
    {
        var today = clock.Today;
        var threshold = await settings.GetExpiringSoonDaysAsync(ct);
        var rows = await db.Members.AsNoTracking()
            .Where(m => m.Type == MemberType.PrivateTrainer)
            .OrderBy(m => m.FirstName).ThenBy(m => m.LastName).ThenBy(m => m.MemberNo)
            .Select(m => new
            {
                m.Id,
                m.MemberNo,
                m.FirstName,
                m.FatherName,
                m.LastName,
                m.Phone,
                m.PhotoFile,
                ClientCount = m.Clients.Count,
                MembershipEnd = m.Subscriptions.Where(s => s.Kind == SubscriptionKind.Membership).Max(s => (DateOnly?)s.EndDate),
                ServiceEnd = m.Subscriptions.Where(s => s.Kind == SubscriptionKind.PrivateTraining).Max(s => (DateOnly?)s.EndDate),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new PrivateTrainerDto(
                r.Id,
                r.MemberNo,
                MemberNumber.Format(r.MemberNo),
                Member.JoinNames(r.FirstName, r.FatherName, r.LastName),
                r.Phone,
                Dto.PhotoUrl(r.Id, r.PhotoFile),
                r.ClientCount,
                Dto.Status(r.MembershipEnd, today, threshold),
                Dto.Status(r.ServiceEnd, today, threshold)))
            .ToList();
    }

    public async Task<IReadOnlyList<MemberSummary>> ListClientsAsync(Guid trainerId, CancellationToken ct = default)
    {
        await EnsureExistsAsync(trainerId, ct);
        var threshold = await settings.GetExpiringSoonDaysAsync(ct);
        return await db.Members.AsNoTracking()
            .Where(m => m.PrivateTrainerId == trainerId)
            .OrderBy(m => m.FirstName).ThenBy(m => m.LastName).ThenBy(m => m.MemberNo)
            .ToSummariesAsync(clock.Today, threshold, ct);
    }

    public async Task LinkClientAsync(Guid trainerId, Guid clientId, CancellationToken ct = default)
    {
        var trainer = await FindAsync(trainerId, ct);
        var client = await FindAsync(clientId, ct);
        client.LinkToPrivateTrainer(trainer);
        client.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task UnlinkClientAsync(Guid trainerId, Guid clientId, CancellationToken ct = default)
    {
        var client = await FindAsync(clientId, ct);
        if (client.PrivateTrainerId != trainerId)
        {
            throw new NotFoundException("client_not_linked", "This member is not a client of this private trainer.");
        }

        client.UnlinkPrivateTrainer();
        client.UpdatedAt = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    private async Task<SubscriptionDto> AddSubscriptionAsync(
        Guid memberId, SubscriptionKind kind, int months, long price, int? planId, int staffId, Action<Member>? check, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // Lock the member row: two stations renewing at the same moment chain one after the other.
        var member = (await db.Members.FromSql($"SELECT * FROM members WHERE id = {memberId} FOR UPDATE").ToListAsync(ct))
            .SingleOrDefault() ?? throw NotFound(memberId);
        check?.Invoke(member);

        var currentEnd = await db.Subscriptions
            .Where(s => s.MemberId == memberId && s.Kind == kind)
            .MaxAsync(s => (DateOnly?)s.EndDate, ct);
        var now = clock.UtcNow;
        var subscription = Subscription.Start(memberId, kind, months, price, currentEnd, clock.Today, staffId, now, planId);
        db.Subscriptions.Add(subscription);
        member.UpdatedAt = now;
        await db.SaveChangesOrConflictAsync(ct);
        await transaction.CommitAsync(ct);

        return await ProjectSubscriptions(db.Subscriptions.AsNoTracking().Where(s => s.Id == subscription.Id)).SingleAsync(ct);
    }

    private static IQueryable<SubscriptionDto> ProjectSubscriptions(IQueryable<Subscription> query) =>
        query.Select(s => new SubscriptionDto(
            s.Id, s.MemberId, s.Kind, s.PlanId, s.Months, s.StartDate, s.EndDate, s.PricePaid, s.CreatedById, s.CreatedBy!.FullName, s.CreatedAt));

    private static void ApplyDetails(
        Member member, string? firstName, string? lastName, string? fatherName, string? motherName,
        DateOnly? birthDate, string? birthPlace, string? nationalNo, string? phone, string? notes)
    {
        member.FirstName = Guard.Required(firstName, "First name", Member.NameMaxLength);
        member.LastName = Guard.Required(lastName, "Surname", Member.NameMaxLength);
        member.FatherName = Guard.Optional(fatherName, "Father's name", Member.NameMaxLength);
        member.MotherName = Guard.Optional(motherName, "Mother's name", Member.NameMaxLength);
        member.BirthDate = birthDate;
        member.BirthPlace = Guard.Optional(birthPlace, "Birth place", Member.NameMaxLength);
        var national = Guard.Optional(nationalNo, "National number", Member.NationalNoMaxLength);
        member.NationalNo = national is null ? null : SearchText.NormalizeDigits(national).Replace(" ", "", StringComparison.Ordinal);
        member.Phone = Guard.Optional(PhoneNumber.Normalize(phone), "Phone", Member.PhoneMaxLength);
        member.Notes = Guard.Optional(notes, "Notes", Member.NotesMaxLength);
        member.RefreshSearchName();
    }

    private async Task EnsureNationalNoFreeAsync(string? nationalNo, Guid memberId, CancellationToken ct)
    {
        if (nationalNo is not null && await db.Members.AnyAsync(m => m.NationalNo == nationalNo && m.Id != memberId, ct))
        {
            throw DbErrors.ToConflict("ux_members_national_no");
        }
    }

    private async Task<Member> FindAsync(Guid id, CancellationToken ct) =>
        await db.Members.FindAsync([id], ct) ?? throw NotFound(id);

    private async Task<Plan> FindActivePlanAsync(int planId, CancellationToken ct)
    {
        var plan = await db.Plans.FindAsync([planId], ct);
        return plan is { IsActive: true }
            ? plan
            : throw new NotFoundException("plan_not_found", $"Plan {planId} does not exist or is not active.");
    }

    private static NotFoundException NotFound(Guid id) => new("member_not_found", $"Member {id} does not exist.");
}
