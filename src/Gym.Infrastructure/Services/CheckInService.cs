using System.Globalization;

namespace Gym.Infrastructure.Services;

/// <summary>Check-in at reception by QR code or member number. Every scan is logged.</summary>
public sealed class CheckInService(GymDbContext db, GymClock clock, SettingsService settings)
{
    public const int MaxListSize = 1000;

    public async Task<CheckInResultDto> CheckInAsync(CheckInRequest request, int? staffId, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var station = Guard.Required(request.Station, "Station", CheckIn.StationMaxLength);
        var hasCode = !string.IsNullOrWhiteSpace(request.Code);
        if (hasCode == request.MemberNo.HasValue)
        {
            throw new DomainException("validation", "Give either the QR code or the member number.");
        }

        CheckInMethod method;
        string scanned;
        Member? member;
        if (hasCode)
        {
            method = CheckInMethod.QrCode;
            scanned = MemberQrCode.Normalize(request.Code);
            if (scanned.Length > CheckIn.ScannedValueMaxLength)
            {
                scanned = scanned[..CheckIn.ScannedValueMaxLength];
            }

            var code = scanned;
            member = await db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.QrCode == code, ct);
        }
        else
        {
            method = CheckInMethod.MemberNo;
            var memberNo = request.MemberNo.GetValueOrDefault();
            scanned = memberNo.ToString(CultureInfo.InvariantCulture);
            member = await db.Members.AsNoTracking().FirstOrDefaultAsync(m => m.MemberNo == memberNo, ct);
        }

        var today = clock.Today;
        var threshold = await settings.GetExpiringSoonDaysAsync(ct);
        DateOnly? membershipEnd = null;
        DateOnly? serviceEnd = null;
        if (member is not null)
        {
            var memberId = member.Id;
            var ends = await db.Subscriptions.AsNoTracking()
                .Where(s => s.MemberId == memberId)
                .GroupBy(s => s.Kind)
                .Select(g => new { Kind = g.Key, End = g.Max(s => s.EndDate) })
                .ToListAsync(ct);
            membershipEnd = ends.Where(e => e.Kind == SubscriptionKind.Membership).Select(e => (DateOnly?)e.End).FirstOrDefault();
            serviceEnd = ends.Where(e => e.Kind == SubscriptionKind.PrivateTraining).Select(e => (DateOnly?)e.End).FirstOrDefault();
        }

        SubscriptionState? state = membershipEnd is { } end ? SubscriptionRules.GetStatus(end, today, threshold) : null;
        var result = member is null ? CheckInResult.UnknownMember : CheckInRules.ResultFor(state);
        var checkIn = new CheckIn
        {
            MemberId = member?.Id,
            At = clock.UtcNow,
            Station = station,
            Method = method,
            ScannedValue = scanned,
            Result = result,
            DaysLeft = state?.DaysLeft ?? 0,
            DaysOverdue = state?.DaysOverdue ?? 0,
            StaffId = staffId,
        };
        db.CheckIns.Add(checkIn);
        await db.SaveChangesAsync(ct);

        var privateService = member?.Type == MemberType.PrivateTrainer ? Dto.Status(serviceEnd, today, threshold) : null;
        return ToDto(checkIn, member, membershipEnd, privateService);
    }

    public async Task<IReadOnlyList<CheckInResultDto>> ListAsync(DateOnly from, DateOnly to, Guid? memberId, CancellationToken ct = default)
    {
        var (start, end) = clock.UtcRange(from, to);
        var query = db.CheckIns.AsNoTracking().Where(c => c.At >= start && c.At < end);
        if (memberId is { } id)
        {
            query = query.Where(c => c.MemberId == id);
        }

        var rows = await query
            .OrderByDescending(c => c.At).ThenByDescending(c => c.Id)
            .Take(MaxListSize)
            .Select(c => new { CheckIn = c, c.Member })
            .ToListAsync(ct);
        return rows.Select(r => ToDto(r.CheckIn, r.Member, null, null)).ToList();
    }

    private static CheckInResultDto ToDto(CheckIn c, Member? member, DateOnly? endDate, SubscriptionStatusDto? privateService) => new(
        c.Id,
        c.Result,
        CheckInRules.IsAdmitted(c.Result),
        c.At,
        c.Station,
        c.Method,
        member?.Id,
        member?.MemberNo,
        member is null ? null : MemberNumber.Format(member.MemberNo),
        member?.FullName,
        member?.Type,
        member is null ? null : Dto.PhotoUrl(member.Id, member.PhotoFile),
        endDate,
        c.DaysLeft,
        c.DaysOverdue,
        privateService);
}
