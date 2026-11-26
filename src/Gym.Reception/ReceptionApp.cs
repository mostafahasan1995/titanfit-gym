using Gym.Reception.Pages;
using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Reception;

/// <summary>
/// The Reception app for the shared shell: name, default station name and the sidebar pages (in order).
/// To add a page: a PageView in Pages/, its strings in ReceptionText, one line below. The smoke screenshots pick it up.
/// </summary>
internal static class ReceptionApp
{
    public static AppDefinition Definition { get; } = new()
    {
        App = GymApp.Reception,
        AppName = ReceptionText.AppName,
        NavLabel = ReceptionText.NavLabel,
        DefaultStationName = "Reception 1",
        Pages =
        [
            new("check-in", UiIcons.Scan, ReceptionText.CheckIn, context => new CheckInPage(context)),
            new("new-member", UiIcons.UserPlus, ReceptionText.NewMember, context => new NewMemberPage(context)),
            new("members", UiIcons.Users, ReceptionText.Members, context => new MembersPage(context)) { Title = ReceptionText.MembersTitle },
            new("private-trainers", UiIcons.Dumbbell, ReceptionText.PrivateTrainers, context => new PrivateTrainersPage(context)),
        ],
    };
}
