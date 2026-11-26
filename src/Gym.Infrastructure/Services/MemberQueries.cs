namespace Gym.Infrastructure.Services;

/// <summary>
/// Member queries shared by search, the dashboard and the reports. A member's status comes from the latest
/// membership end date, checked with <see cref="SubscriptionRules.GetStatus"/>; the SQL filters below use the
/// same boundaries (expired: end &lt; today; expiring soon: today ≤ end ≤ today + threshold; active: later).
/// </summary>
internal static class MemberQueries
{
    public static IQueryable<Member> WhereMembershipStatus(
        this IQueryable<Member> query, SubscriptionStatus status, DateOnly today, int expiringSoonDays)
    {
        var limit = today.AddDays(expiringSoonDays);
        return status switch
        {
            SubscriptionStatus.Expired => query.Where(m =>
                m.Subscriptions.Where(s => s.Kind == SubscriptionKind.Membership).Max(s => (DateOnly?)s.EndDate) < today),
            SubscriptionStatus.ExpiringSoon => query.Where(m =>
                m.Subscriptions.Where(s => s.Kind == SubscriptionKind.Membership).Max(s => (DateOnly?)s.EndDate) >= today
                && m.Subscriptions.Where(s => s.Kind == SubscriptionKind.Membership).Max(s => (DateOnly?)s.EndDate) <= limit),
            _ => query.Where(m =>
                m.Subscriptions.Where(s => s.Kind == SubscriptionKind.Membership).Max(s => (DateOnly?)s.EndDate) > limit),
        };
    }

    /// <summary>Matches a QR code exactly, or a name / phone / national number / member number.</summary>
    public static IQueryable<Member> WhereMatches(this IQueryable<Member> query, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return query;
        }

        var code = MemberQrCode.Normalize(text);
        if (MemberQrCode.IsWellFormed(code))
        {
            return query.Where(m => m.QrCode == code);
        }

        var name = SearchText.Normalize(text);
        var namePattern = "%" + Guard.EscapeLike(name) + "%";
        var phoneKey = PhoneNumber.SearchKey(text);
        var phonePattern = phoneKey.Length >= 3 ? "%" + phoneKey + "%" : null;
        var digits = SearchText.DigitsOnly(text);
        var nationalPattern = digits.Length >= 3 ? "%" + digits + "%" : null;
        var hasMemberNo = MemberNumber.TryParse(text, out var memberNo);

        return query.Where(m =>
            EF.Functions.ILike(m.SearchName, namePattern)
            || (phonePattern != null && m.Phone != null && EF.Functions.ILike(m.Phone, phonePattern))
            || (nationalPattern != null && m.NationalNo != null && EF.Functions.ILike(m.NationalNo, nationalPattern))
            || (hasMemberNo && m.MemberNo == memberNo));
    }

    public static async Task<List<MemberSummary>> ToSummariesAsync(
        this IQueryable<Member> query, DateOnly today, int expiringSoonDays, CancellationToken ct)
    {
        var rows = await query
            .Select(m => new
            {
                m.Id,
                m.MemberNo,
                m.Type,
                m.FirstName,
                m.FatherName,
                m.LastName,
                m.Phone,
                m.NationalNo,
                m.PhotoFile,
                m.PrivateTrainerId,
                MembershipEnd = m.Subscriptions.Where(s => s.Kind == SubscriptionKind.Membership).Max(s => (DateOnly?)s.EndDate),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new MemberSummary(
                r.Id,
                r.MemberNo,
                MemberNumber.Format(r.MemberNo),
                r.Type,
                Member.JoinNames(r.FirstName, r.FatherName, r.LastName),
                r.Phone,
                r.NationalNo,
                Dto.PhotoUrl(r.Id, r.PhotoFile),
                r.PrivateTrainerId,
                Dto.Status(r.MembershipEnd, today, expiringSoonDays)))
            .ToList();
    }
}
