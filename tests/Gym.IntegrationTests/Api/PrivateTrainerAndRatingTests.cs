using System.Net;
using Gym.Client;

namespace Gym.IntegrationTests.Api;

public sealed class PrivateTrainerAndRatingTests(ApiFactory fx) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Private_trainer_pays_the_service_and_gets_clients()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var reception = await fx.ReceptionAsync();
        var trainer = await reception.NewMemberAsync(planMonths: 1, type: MemberType.PrivateTrainer, firstName: "Coach", lastName: "Fadi");
        var client = await reception.NewMemberAsync(planMonths: 3, firstName: "Client", lastName: "One");
        var regular = await reception.NewMemberAsync(planMonths: 1, firstName: "Not", lastName: "Trainer");

        // The private training service: months × the fixed monthly fee (100,000 by default).
        var service = await reception.AddPrivateServiceAsync(trainer.Id, 2);
        Assert.Equal((SubscriptionKind.PrivateTraining, 2, 50m), (service.Kind, service.Months, service.PricePaid));
        Assert.Equal(fx.Today, service.StartDate);
        Assert.Null(service.PlanId);

        var renewal = await reception.AddPrivateServiceAsync(trainer.Id, 1);
        Assert.Equal(service.EndDate.AddDays(1), renewal.StartDate); // early renewal chains

        var notTrainer = await Assert.ThrowsAsync<GymApiException>(() => reception.AddPrivateServiceAsync(regular.Id, 1));
        Assert.Equal("not_a_private_trainer", notTrainer.Code);

        await reception.LinkClientAsync(trainer.Id, client.Id);
        Assert.Equal(trainer.Id, (await reception.GetMemberAsync(client.Id)).PrivateTrainerId);
        Assert.Equal("Coach Omar Fadi", (await reception.GetMemberAsync(client.Id)).PrivateTrainerName);
        var cannotLink = await Assert.ThrowsAsync<GymApiException>(() => reception.LinkClientAsync(regular.Id, client.Id));
        Assert.Equal("not_a_private_trainer", cannotLink.Code);

        var listed = Assert.Single(await reception.GetPrivateTrainersAsync(), t => t.Id == trainer.Id);
        Assert.Equal(1, listed.ClientCount);
        Assert.Equal(SubscriptionStatus.Active, listed.PrivateService?.Status);
        Assert.Equal(renewal.EndDate, listed.PrivateService?.EndDate);
        Assert.Equal(client.Id, Assert.Single(await reception.GetPrivateTrainerClientsAsync(trainer.Id)).Id);

        // The trainer's own membership is separate and shows at the door with the service status.
        var door = await reception.CheckInAsync(new CheckInRequest("Reception 1", Code: trainer.QrCode));
        Assert.Equal(CheckInResult.Active, door.Result);
        Assert.Equal(trainer.Membership!.EndDate, door.EndDate);
        Assert.Equal(renewal.EndDate, door.PrivateService?.EndDate);

        // A trainer with clients cannot become a regular member until they are unlinked.
        var typeChange = await Assert.ThrowsAsync<GymApiException>(() =>
            reception.UpdateMemberAsync(trainer.Id, new UpdateMemberRequest("Coach", "Fadi", FatherName: "Omar", NationalNo: trainer.NationalNo, Type: MemberType.Regular)));
        Assert.Equal((HttpStatusCode.Conflict, "trainer_has_clients"), (typeChange.StatusCode, typeChange.Code));

        await reception.UnlinkClientAsync(trainer.Id, client.Id);
        Assert.Null((await reception.GetMemberAsync(client.Id)).PrivateTrainerId);
        Assert.Empty(await reception.GetPrivateTrainerClientsAsync(trainer.Id));
    }

    [Fact]
    public async Task Private_training_fee_comes_from_the_settings()
    {
        fx.Time.Set(TestTimeProvider.DefaultStart);
        var admin = await fx.AdminAsync();
        var reception = await fx.ReceptionAsync();
        var trainer = await reception.NewMemberAsync(type: MemberType.PrivateTrainer);
        var before = await admin.GetSettingsAsync();

        await admin.UpdateSettingsAsync(before with { PrivateTrainingMonthlyFee = 30m });
        try
        {
            Assert.Equal(90m, (await reception.AddPrivateServiceAsync(trainer.Id, 3)).PricePaid);
        }
        finally
        {
            await admin.UpdateSettingsAsync(before);
        }

        var invalid = await Assert.ThrowsAsync<GymApiException>(() => admin.UpdateSettingsAsync(new SettingsDto(-1, 1)));
        Assert.Equal("invalid_setting", invalid.Code);
    }

    [Fact]
    public async Task Club_trainer_gets_one_rating_per_month_with_history()
    {
        var admin = await fx.AdminAsync();
        var coach = await admin.CreateStaffAsync(new CreateStaffRequest("Coach Lina", StaffType.ClubTrainer));

        await admin.SetRatingAsync(coach.Id, 2026, 11, new SetRatingRequest(4, "Good classes"));
        var changed = await admin.SetRatingAsync(coach.Id, 2026, 11, new SetRatingRequest(5, "Excellent"));
        await admin.SetRatingAsync(coach.Id, 2026, 10, new SetRatingRequest(3));

        Assert.Equal((5, "Excellent", "Owner"), (changed.Stars, changed.Note, changed.RatedBy));
        var history = await admin.GetRatingsAsync(coach.Id);
        Assert.Equal(new[] { (2026, 11, 5), (2026, 10, 3) }, history.Select(r => (r.Year, r.Month, r.Stars)));
        Assert.Contains(await admin.GetMonthRatingsAsync(2026, 11), r => r.StaffId == coach.Id && r.Stars == 5);

        var cashier = await admin.CreateStaffAsync(new CreateStaffRequest("Cashier Noor", StaffType.Cashier));
        Assert.Equal("not_a_club_trainer", (await Assert.ThrowsAsync<GymApiException>(() =>
            admin.SetRatingAsync(cashier.Id, 2026, 11, new SetRatingRequest(4)))).Code);
        Assert.Equal("invalid_stars", (await Assert.ThrowsAsync<GymApiException>(() =>
            admin.SetRatingAsync(coach.Id, 2026, 9, new SetRatingRequest(6)))).Code);
        Assert.Equal("invalid_month", (await Assert.ThrowsAsync<GymApiException>(() =>
            admin.SetRatingAsync(coach.Id, 2026, 13, new SetRatingRequest(3)))).Code);

        var reception = await fx.ReceptionAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await Assert.ThrowsAsync<GymApiException>(() =>
            reception.SetRatingAsync(coach.Id, 2026, 11, new SetRatingRequest(1)))).StatusCode);
    }
}
