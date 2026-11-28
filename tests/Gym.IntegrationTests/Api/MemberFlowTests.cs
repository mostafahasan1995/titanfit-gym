using System.Net;
using Gym.Client;

namespace Gym.IntegrationTests.Api;

public sealed class MemberFlowTests(ApiFactory fx) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Register_with_photo_then_check_in_by_qr_and_by_member_number()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var reception = await fx.ReceptionAsync();
        var planId = await reception.PlanIdAsync(1);

        var member = await reception.CreateMemberAsync(new CreateMemberRequest(
            "محمد", "الأحمد", FatherName: "علي", MotherName: "سلمى", BirthDate: new DateOnly(1998, 4, 12), BirthPlace: "دمشق",
            NationalNo: TestData.NationalNo(), Phone: "+963 944 000 111", PlanId: planId));

        Assert.True(member.MemberNo > 0);
        Assert.Matches(@"^#\d{5,}$", member.MemberNoText);
        Assert.True(MemberQrCode.IsWellFormed(member.QrCode));
        Assert.DoesNotContain(member.NationalNo!, member.QrCode, StringComparison.Ordinal);
        Assert.Equal(SubscriptionStatus.Active, member.Membership?.Status);
        Assert.Equal(fx.Today.AddMonths(1), member.Membership?.EndDate);
        Assert.Null(member.PhotoUrl);

        await reception.UploadMemberPhotoAsync(member.Id, TestData.TinyJpeg, "image/jpeg");
        var withPhoto = await reception.GetMemberAsync(member.Id);
        Assert.NotNull(withPhoto.PhotoUrl);
        Assert.Equal(TestData.TinyJpeg, await reception.GetBytesAsync(withPhoto.PhotoUrl!));
        Assert.Equal(TestData.TinyJpeg, await reception.GetMemberPhotoAsync(member.Id));

        var byQr = await reception.CheckInAsync(new CheckInRequest("Reception 1", Code: member.QrCode));
        Assert.Equal(CheckInResult.Active, byQr.Result);
        Assert.True(byQr.Admitted);
        Assert.Equal(member.Id, byQr.MemberId);
        Assert.Equal("محمد علي الأحمد", byQr.FullName);
        Assert.Equal(withPhoto.PhotoUrl, byQr.PhotoUrl);
        Assert.Equal(fx.Today.AddMonths(1).DayNumber - fx.Today.DayNumber, byQr.DaysLeft);
        Assert.Equal(CheckInMethod.QrCode, byQr.Method);

        var byNumber = await reception.CheckInAsync(new CheckInRequest("Reception 2", MemberNo: member.MemberNo));
        Assert.Equal(CheckInResult.Active, byNumber.Result);
        Assert.Equal(CheckInMethod.MemberNo, byNumber.Method);

        var log = await reception.GetCheckInsAsync(memberId: member.Id);
        Assert.Equal(new[] { "Reception 2", "Reception 1" }, log.Select(c => c.Station));
    }

    [Fact]
    public async Task Reissued_qr_code_replaces_the_old_one_at_the_door()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var reception = await fx.ReceptionAsync();
        var member = await reception.NewMemberAsync();

        var reissued = await reception.ReissueQrCodeAsync(member.Id);

        Assert.NotEqual(member.QrCode, reissued.QrCode);
        var old = await reception.CheckInAsync(new CheckInRequest("Reception 1", Code: member.QrCode));
        Assert.Equal(CheckInResult.UnknownMember, old.Result);
        Assert.False(old.Admitted);
        Assert.Null(old.MemberId);
        Assert.Equal(CheckInResult.Active, (await reception.CheckInAsync(new CheckInRequest("Reception 1", Code: reissued.QrCode))).Result);
    }

    [Fact]
    public async Task Status_at_the_door_follows_the_end_date()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var reception = await fx.ReceptionAsync();
        var member = await reception.NewMemberAsync(planMonths: 1);
        var end = member.Membership!.EndDate;

        fx.Time.SetToday(end.AddDays(-4));
        Assert.Equal(CheckInResult.Active, (await CheckInAsync(reception, member)).Result);

        fx.Time.SetToday(end.AddDays(-3));
        var soon = await CheckInAsync(reception, member);
        Assert.Equal((CheckInResult.ExpiringSoon, 3), (soon.Result, soon.DaysLeft));
        Assert.True(soon.Admitted);

        fx.Time.SetToday(end);
        var today = await CheckInAsync(reception, member);
        Assert.Equal((CheckInResult.ExpiringSoon, 0), (today.Result, today.DaysLeft)); // expires today: still valid

        fx.Time.SetToday(end.AddDays(5));
        var expired = await CheckInAsync(reception, member);
        Assert.Equal((CheckInResult.Expired, 5), (expired.Result, expired.DaysOverdue));
        Assert.False(expired.Admitted);

        // Renewing after expiry starts today.
        var renewal = await reception.RenewMemberAsync(member.Id, await reception.PlanIdAsync(1));
        Assert.Equal(end.AddDays(5), renewal.StartDate);
        Assert.Equal(CheckInResult.Active, (await CheckInAsync(reception, member)).Result);
        fx.Time.Set(TestTimeProvider.DefaultStart);
    }

    [Fact]
    public async Task Early_renewal_starts_the_day_after_the_current_end_date()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var reception = await fx.ReceptionAsync();
        var member = await reception.NewMemberAsync(planMonths: 1);
        var firstEnd = member.Membership!.EndDate;

        var renewal = await reception.RenewMemberAsync(member.Id, await reception.PlanIdAsync(3));

        Assert.Equal(firstEnd.AddDays(1), renewal.StartDate);
        Assert.Equal(firstEnd.AddDays(1).AddMonths(3), renewal.EndDate);
        Assert.Equal(3, renewal.Months);
        Assert.Equal(55m, renewal.PricePaid);
        Assert.Equal(renewal.EndDate, (await reception.GetMemberAsync(member.Id)).Membership?.EndDate);
        var history = await reception.GetMemberSubscriptionsAsync(member.Id);
        Assert.Equal(new[] { renewal.StartDate, fx.Today }, history.Select(s => s.StartDate));
    }

    [Fact]
    public async Task Member_without_a_subscription_is_not_admitted()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var reception = await fx.ReceptionAsync();
        var member = await reception.NewMemberAsync(planMonths: null);

        Assert.Null(member.Membership);
        Assert.Equal(CheckInResult.NoSubscription, (await CheckInAsync(reception, member)).Result);
    }

    [Fact]
    public async Task Search_finds_members_by_text_and_status()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var reception = await fx.ReceptionAsync();
        var member = await reception.CreateMemberAsync(new CreateMemberRequest(
            "إبراهيم", "القاسم", NationalNo: TestData.NationalNo(), Phone: "0955 123 987", PlanId: await reception.PlanIdAsync(1)));

        Assert.Contains((await reception.SearchMembersAsync("ابراهيم")).Items, m => m.Id == member.Id);
        Assert.Contains((await reception.SearchMembersAsync("955123987")).Items, m => m.Id == member.Id);
        Assert.Contains((await reception.SearchMembersAsync(member.NationalNo)).Items, m => m.Id == member.Id);
        Assert.Contains((await reception.SearchMembersAsync(member.MemberNoText)).Items, m => m.Id == member.Id);
        Assert.Contains((await reception.SearchMembersAsync(member.QrCode)).Items, m => m.Id == member.Id);
        Assert.Contains((await reception.SearchMembersAsync(status: SubscriptionStatus.Active, pageSize: 100)).Items, m => m.Id == member.Id);
        Assert.DoesNotContain((await reception.SearchMembersAsync(status: SubscriptionStatus.Expired, pageSize: 100)).Items, m => m.Id == member.Id);

        var page = await reception.SearchMembersAsync(pageSize: 1);
        Assert.Single(page.Items);
        Assert.True(page.Total >= 1);
    }

    [Fact]
    public async Task Bad_input_and_duplicates_get_clear_errors()
    {
        var reception = await fx.ReceptionAsync();
        var member = await reception.NewMemberAsync();

        var duplicate = await Assert.ThrowsAsync<GymApiException>(() =>
            reception.CreateMemberAsync(new CreateMemberRequest("Other", "Person", NationalNo: member.NationalNo)));
        Assert.Equal((HttpStatusCode.Conflict, "duplicate_national_no"), (duplicate.StatusCode, duplicate.Code));

        var noName = await Assert.ThrowsAsync<GymApiException>(() => reception.CreateMemberAsync(new CreateMemberRequest(" ", "Person")));
        Assert.Equal((HttpStatusCode.BadRequest, "validation"), (noName.StatusCode, noName.Code));

        var badPlan = await Assert.ThrowsAsync<GymApiException>(() => reception.RenewMemberAsync(member.Id, 999_999));
        Assert.Equal((HttpStatusCode.NotFound, "plan_not_found"), (badPlan.StatusCode, badPlan.Code));

        var notFound = await Assert.ThrowsAsync<GymApiException>(() => reception.GetMemberAsync(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);

        var badPhoto = await Assert.ThrowsAsync<GymApiException>(() => reception.UploadMemberPhotoAsync(member.Id, [1, 2, 3, 4], "image/jpeg"));
        Assert.Equal("invalid_photo", badPhoto.Code);

        var updated = await reception.UpdateMemberAsync(member.Id, new UpdateMemberRequest("Sami", "Haddad", Phone: "0999 888 777", NationalNo: member.NationalNo));
        Assert.Equal("0999888777", updated.Phone);
    }

    private static Task<CheckInResultDto> CheckInAsync(GymApiClient client, MemberDto member) =>
        client.CheckInAsync(new CheckInRequest("Reception 1", Code: member.QrCode));
}
