using Gym.Domain;
using Gym.UI.Shell;

namespace Gym.Tests.Foundation;

public sealed class AppRolesTests
{
    [Theory]
    [InlineData(GymApp.Reception, StaffRole.Reception, true)]
    [InlineData(GymApp.Reception, StaffRole.Admin, true)]
    [InlineData(GymApp.Reception, StaffRole.Cashier, false)]
    [InlineData(GymApp.Cafeteria, StaffRole.Cashier, true)]
    [InlineData(GymApp.Cafeteria, StaffRole.Admin, true)]
    [InlineData(GymApp.Cafeteria, StaffRole.Reception, false)]
    [InlineData(GymApp.Admin, StaffRole.Admin, true)]
    [InlineData(GymApp.Admin, StaffRole.Reception, false)]
    [InlineData(GymApp.Admin, StaffRole.Cashier, false)]
    public void Each_app_lets_in_only_its_roles(GymApp app, StaffRole role, bool allowed)
    {
        Assert.Equal(allowed, AppRoles.CanUse(app, role));
    }

    [Theory]
    [InlineData(GymApp.Admin)]
    [InlineData(GymApp.Reception)]
    [InlineData(GymApp.Cafeteria)]
    public void A_login_without_a_role_cannot_use_any_app(GymApp app)
    {
        Assert.False(AppRoles.CanUse(app, null));
    }

    [Fact]
    public void Only_the_station_apps_remember_the_username()
    {
        Assert.False(AppRoles.RemembersUsername(GymApp.Admin));
        Assert.True(AppRoles.RemembersUsername(GymApp.Reception));
        Assert.True(AppRoles.RemembersUsername(GymApp.Cafeteria));
    }

    [Fact]
    public void Each_app_has_its_own_refusal_message()
    {
        var messages = Enum.GetValues<GymApp>().Select(AppRoles.DeniedMessage).ToList();

        Assert.Equal(messages.Count, messages.Distinct().Count());
        Assert.All(messages, m => Assert.False(string.IsNullOrWhiteSpace(m.Ar)));
    }

    [Fact]
    public void The_first_allowed_role_is_the_app_s_own_staff()
    {
        Assert.Equal(StaffRole.Reception, AppRoles.Allowed(GymApp.Reception)[0]);
        Assert.Equal(StaffRole.Cashier, AppRoles.Allowed(GymApp.Cafeteria)[0]);
        Assert.Equal(StaffRole.Admin, AppRoles.Allowed(GymApp.Admin)[0]);
    }
}
