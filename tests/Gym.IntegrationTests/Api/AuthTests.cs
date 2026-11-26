using System.Net;
using Gym.Client;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Gym.IntegrationTests.Api;

public sealed class FirstRunSetupTests(ApiFactory fx) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task First_admin_is_created_once_through_the_setup()
    {
        var anonymous = fx.Anonymous();
        Assert.True((await anonymous.GetSetupStatusAsync()).NeedsSetup);

        var login = await anonymous.SetupAdminAsync(new SetupAdminRequest("Owner", "Owner", "first-run-test"));

        Assert.Equal("owner", login.Staff.Username);
        Assert.Equal(StaffRole.Admin, login.Staff.Role);
        Assert.False((await fx.Anonymous().GetSetupStatusAsync()).NeedsSetup);
        var again = await Assert.ThrowsAsync<GymApiException>(() =>
            fx.Anonymous().SetupAdminAsync(new SetupAdminRequest("Intruder", "intruder", "intruder-pass")));
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("already_set_up", again.Code);

        var client = fx.Anonymous();
        await client.LoginAsync("OWNER", "first-run-test");
        Assert.Equal("Owner", (await client.GetMeAsync()).FullName);
    }
}

public sealed class AuthTests(ApiFactory fx) : IClassFixture<ApiFactory>
{
    private static readonly string[] AnonymousRoutes = ["/health", "/api/auth/login", "/api/setup/status", "/api/setup/admin"];

    [Fact]
    public void Every_endpoint_states_who_may_call_it()
    {
        var endpoints = fx.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        Assert.True(endpoints.Count > 50, $"Only {endpoints.Count} endpoints found.");

        foreach (var endpoint in endpoints)
        {
            var route = "/" + endpoint.RoutePattern.RawText?.Trim('/');
            if (endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            {
                Assert.Contains(route, AnonymousRoutes);
                continue;
            }

            var policies = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Select(a => a.Policy).ToList();
            Assert.True(policies.Count > 0 && policies.All(p => !string.IsNullOrEmpty(p)), $"{endpoint.DisplayName} has no role policy.");
        }
    }

    [Fact]
    public async Task Wrong_password_is_refused()
    {
        await fx.AdminAsync();

        var error = await Assert.ThrowsAsync<GymApiException>(() => fx.Anonymous().LoginAsync(ApiFactory.AdminUsername, "wrong-password"));

        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        Assert.Equal("invalid_login", error.Code);
    }

    [Fact]
    public async Task Login_returns_a_token_with_the_role()
    {
        var (client, staff) = await fx.StaffAsync(StaffRole.Cashier);

        var me = await client.GetMeAsync();

        Assert.Equal(staff.Id, me.Id);
        Assert.Equal(StaffRole.Cashier, me.Role);
        Assert.NotNull(me.LastLoginAt);
    }

    [Fact]
    public async Task Each_role_reaches_only_its_own_endpoints()
    {
        var admin = await fx.AdminAsync();
        var reception = await fx.ReceptionAsync();
        var cashier = await fx.CashierAsync();
        var categoryId = (await admin.GetCategoriesAsync()).First().Id;

        // A cashier cannot register members, check members in, see the dashboard or manage staff.
        await AssertForbiddenAsync(() => cashier.CreateMemberAsync(new CreateMemberRequest("No", "Way")));
        await AssertForbiddenAsync(() => cashier.CheckInAsync(new CheckInRequest("Cafeteria 1", MemberNo: 1)));
        await AssertForbiddenAsync(() => cashier.GetDashboardAsync());
        await AssertForbiddenAsync(() => cashier.GetStaffAsync());
        await AssertForbiddenAsync(() => cashier.UpdateSettingsAsync(new SettingsDto(3, 100_000)));

        // Reception cannot sell, manage stock or products, or see reports.
        await AssertForbiddenAsync(() => reception.CreateSaleAsync(new CreateSaleRequest("Reception 1", [new(1, 1)])));
        await AssertForbiddenAsync(() => reception.StockInAsync(new StockInRequest(1, 1)));
        await AssertForbiddenAsync(() => reception.CreateProductAsync(new SaveProductRequest("X", "X", categoryId, 1)));
        await AssertForbiddenAsync(() => reception.GetSalesReportAsync(fx.Today, fx.Today));
        await AssertForbiddenAsync(() => reception.CreatePlanAsync(new SavePlanRequest(2, 1)));

        // What each one needs works.
        Assert.NotEmpty(await reception.GetPlansAsync());
        await reception.SearchMembersAsync();
        Assert.NotEmpty(await cashier.GetCategoriesAsync());
        await cashier.GetProductsAsync();
        await cashier.GetStockAsync();
        Assert.Equal(3, (await cashier.GetSettingsAsync()).ExpiringSoonDays);
        await admin.GetDashboardAsync();
        await admin.SearchMembersAsync();
        await admin.GetStockAsync();
    }

    [Fact]
    public async Task Deactivated_staff_loses_access_at_once()
    {
        var admin = await fx.AdminAsync();
        var (reception, staff) = await fx.StaffAsync(StaffRole.Reception);
        await reception.SearchMembersAsync();

        await admin.UpdateStaffAsync(staff.Id, new UpdateStaffRequest(staff.FullName, staff.Type, IsActive: false, Username: staff.Username, Role: staff.Role));

        var error = await Assert.ThrowsAsync<GymApiException>(() => reception.SearchMembersAsync());
        Assert.Equal(HttpStatusCode.Unauthorized, error.StatusCode);
        await Assert.ThrowsAsync<GymApiException>(() => fx.Anonymous().LoginAsync(staff.Username!, ApiFactory.StaffPassword));
    }

    [Fact]
    public async Task Password_reset_replaces_the_password_and_old_tokens()
    {
        var admin = await fx.AdminAsync();
        var (cashier, staff) = await fx.StaffAsync(StaffRole.Cashier);

        await admin.ResetStaffPasswordAsync(staff.Id, "new-test-password");

        await Assert.ThrowsAsync<GymApiException>(() => cashier.GetProductsAsync());
        await Assert.ThrowsAsync<GymApiException>(() => fx.Anonymous().LoginAsync(staff.Username!, ApiFactory.StaffPassword));
        var fresh = fx.Anonymous();
        await fresh.LoginAsync(staff.Username!, "new-test-password");
        await fresh.GetProductsAsync();

        await fresh.ChangePasswordAsync("new-test-password", "own-test-password");
        await fx.Anonymous().LoginAsync(staff.Username!, "own-test-password");
    }

    [Fact]
    public async Task Club_trainer_has_no_login_unless_given_one()
    {
        var admin = await fx.AdminAsync();

        var trainer = await admin.CreateStaffAsync(new CreateStaffRequest("Coach Rami", StaffType.ClubTrainer, Phone: "0933 111 222"));

        Assert.False(trainer.CanLogIn);
        Assert.Null(trainer.Username);
        Assert.Equal("0933111222", trainer.Phone);
        var noRole = await Assert.ThrowsAsync<GymApiException>(() =>
            admin.UpdateStaffAsync(trainer.Id, new UpdateStaffRequest(trainer.FullName, StaffType.ClubTrainer, Username: "coach.rami")));
        Assert.Equal("role_required", noRole.Code);

        var withLogin = await admin.UpdateStaffAsync(trainer.Id, new UpdateStaffRequest(trainer.FullName, StaffType.ClubTrainer, Username: "coach.rami", Role: StaffRole.Reception));
        Assert.False(withLogin.CanLogIn); // no password yet
        await admin.ResetStaffPasswordAsync(trainer.Id, "coach-test-pass");
        var coach = fx.Anonymous();
        await coach.LoginAsync("coach.rami", "coach-test-pass");
        Assert.Equal(StaffRole.Reception, (await coach.GetMeAsync()).Role);
    }

    [Fact]
    public async Task Last_admin_cannot_lock_everyone_out()
    {
        var admin = await fx.AdminAsync();
        var me = await admin.GetMeAsync();

        var error = await Assert.ThrowsAsync<GymApiException>(() =>
            admin.UpdateStaffAsync(me.Id, new UpdateStaffRequest(me.FullName, me.Type, IsActive: false, Username: me.Username, Role: me.Role)));

        Assert.Equal(HttpStatusCode.Conflict, error.StatusCode);
        Assert.Equal("last_admin", error.Code);
        await admin.GetMeAsync();
    }

    [Fact]
    public async Task Duplicate_username_is_a_conflict()
    {
        var admin = await fx.AdminAsync();
        await admin.CreateStaffAsync(new CreateStaffRequest("Sara", StaffType.Cashier, Username: "sara.dup", Password: "sara-test-pass"));

        var error = await Assert.ThrowsAsync<GymApiException>(() =>
            admin.CreateStaffAsync(new CreateStaffRequest("Sara 2", StaffType.Cashier, Username: "SARA.dup", Password: "sara-test-pass")));

        Assert.Equal("duplicate_username", error.Code);
    }

    private static async Task AssertForbiddenAsync(Func<Task> call)
    {
        var error = await Assert.ThrowsAsync<GymApiException>(call);
        Assert.Equal(HttpStatusCode.Forbidden, error.StatusCode);
    }
}
