namespace Gym.IntegrationTests.Database;

/// <summary>Search and status filters in SQL agree with <see cref="SubscriptionRules.GetStatus"/>.</summary>
public sealed class MemberQueryTests(PostgresFixture fx) : IClassFixture<PostgresFixture>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(TestTimeProvider.DefaultStart.UtcDateTime);

    [Fact]
    public async Task Status_filters_match_get_status_at_every_edge()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var staffId = await fx.AddStaffAsync();
        var ends = new Dictionary<Guid, DateOnly?>();
        foreach (var offset in new int?[] { -30, -1, 0, 1, 3, 4, 40, null })
        {
            var member = PostgresFixture.NewMember(firstName: "Status", lastName: offset?.ToString() ?? "none");
            await using var db = fx.CreateContext();
            db.Members.Add(member);
            if (offset is { } days)
            {
                var end = Today.AddDays(days);
                db.Subscriptions.Add(new Subscription
                {
                    MemberId = member.Id, Months = 1, StartDate = end.AddMonths(-1), EndDate = end, CreatedById = staffId, CreatedAt = DateTime.UtcNow,
                });
            }

            await db.SaveChangesAsync();
            ends[member.Id] = offset is { } d ? Today.AddDays(d) : null;
        }

        await using var query = fx.CreateContext();
        foreach (var status in Enum.GetValues<SubscriptionStatus>())
        {
            var ids = ends.Keys.ToList();
            var found = await query.Members.WhereMembershipStatus(status, Today, 3).Where(m => ids.Contains(m.Id)).Select(m => m.Id).ToListAsync();
            var expected = ends.Where(e => e.Value is { } end && SubscriptionRules.GetStatus(end, Today, 3).Status == status).Select(e => e.Key);
            Assert.Equal(expected.Order(), found.Order());
        }
    }

    [Fact]
    public async Task Latest_membership_decides_and_private_service_is_separate()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var staffId = await fx.AddStaffAsync();
        var member = PostgresFixture.NewMember(firstName: "Chain");
        member.Type = MemberType.PrivateTrainer;
        await using (var db = fx.CreateContext())
        {
            db.Members.Add(member);
            db.Subscriptions.AddRange(
                new Subscription { MemberId = member.Id, Months = 1, StartDate = Today.AddDays(-70), EndDate = Today.AddDays(-40), CreatedById = staffId, CreatedAt = DateTime.UtcNow },
                new Subscription { MemberId = member.Id, Months = 1, StartDate = Today.AddDays(-5), EndDate = Today.AddDays(25), CreatedById = staffId, CreatedAt = DateTime.UtcNow },
                new Subscription { MemberId = member.Id, Kind = SubscriptionKind.PrivateTraining, Months = 1, StartDate = Today.AddDays(-40), EndDate = Today.AddDays(-10), CreatedById = staffId, CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        await using var read = fx.CreateContext();
        var dto = await fx.Members(read).GetAsync(member.Id);

        Assert.Equal(SubscriptionStatus.Active, dto.Membership?.Status);
        Assert.Equal(Today.AddDays(25), dto.Membership?.EndDate);
        Assert.Equal(25, dto.Membership?.DaysLeft);
        Assert.Equal(SubscriptionStatus.Expired, dto.PrivateService?.Status);
        Assert.Equal(10, dto.PrivateService?.DaysOverdue);
    }

    [Fact]
    public async Task Search_by_arabic_name_phone_national_number_member_number_and_qr()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var staffId = await fx.AddStaffAsync();
        MemberDto created;
        await using (var db = fx.CreateContext())
        {
            created = await fx.Members(db).CreateAsync(
                new CreateMemberRequest("أحمد", "الحدّاد", FatherName: "خالد", MotherName: "فاطمة", NationalNo: "٠٣٠١٢٣٤٥٦٧٨", Phone: "+963 944 555 101"),
                staffId);
        }

        Assert.Equal("03012345678", created.NationalNo);
        Assert.Equal("+963944555101", created.Phone);

        foreach (var text in new[] { "احمد", "إحمد خالد", "الحداد", "فاطمه", "0944 555 101", "944555101", "03012345678", created.MemberNoText, created.QrCode.ToLowerInvariant() })
        {
            await using var db = fx.CreateContext();
            var result = await fx.Members(db).SearchAsync(text, null, null, 1, 50);
            Assert.True(result.Items.Any(m => m.Id == created.Id), $"Search for '{text}' did not find the member.");
        }

        await using (var db = fx.CreateContext())
        {
            var none = await fx.Members(db).SearchAsync("لا يوجد هذا الاسم", null, null, 1, 50);
            Assert.DoesNotContain(none.Items, m => m.Id == created.Id);
        }
    }

    [Fact]
    public async Task Renewals_from_two_stations_at_once_chain_without_gaps_or_overlaps()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var staffId = await fx.AddStaffAsync();
        int planId;
        Guid memberId;
        await using (var db = fx.CreateContext())
        {
            planId = (await db.Plans.SingleAsync(p => p.Months == 1)).Id;
            memberId = (await fx.Members(db).CreateAsync(new CreateMemberRequest("Renew", "Twice", PlanId: planId), staffId)).Id;
        }

        await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            await using var db = fx.CreateContext();
            await fx.Members(db).RenewAsync(memberId, planId, staffId);
        }));

        await using var read = fx.CreateContext();
        var periods = await read.Subscriptions.Where(s => s.MemberId == memberId).OrderBy(s => s.StartDate).ToListAsync();
        Assert.Equal(3, periods.Count);
        Assert.Equal(Today, periods[0].StartDate);
        Assert.Equal(periods[0].EndDate.AddDays(1), periods[1].StartDate);
        Assert.Equal(periods[1].EndDate.AddDays(1), periods[2].StartDate);
    }
}
