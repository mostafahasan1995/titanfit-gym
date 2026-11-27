using Gym.Domain;
using Gym.Reception.Pages;
using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Reception;

/// <summary>
/// The Reception app for the shared shell: name, default station name and the sidebar pages (in order).
/// To add a page: a PageView in Pages/, its strings in ReceptionText, one line below. The smoke screenshots pick it up,
/// and <see cref="AppDefinition.SmokeStates"/> adds screenshots of more states (other check-in results, a filled form, dialogs).
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
        SmokeStates =
        [
            new("check-in-expiring", "check-in", view => ((CheckInPage)view).ShowSampleAsync(SubscriptionStatus.ExpiringSoon)),
            new("check-in-expired", "check-in", view => ((CheckInPage)view).ShowSampleAsync(SubscriptionStatus.Expired)),
            new("new-member-filled", "new-member", view => ((NewMemberPage)view).FillSampleAsync()),
            new("new-member-saved", "new-member", view => ((NewMemberPage)view).SaveSampleAsync()),
            new("members-renew", "members", view => ((MembersPage)view).ShowRenewSampleAsync()),
            new("members-qr", "members", view => ((MembersPage)view).ShowQrSampleAsync()),
            new("private-trainers-pay", "private-trainers", view => ((PrivateTrainersPage)view).ShowPaySampleAsync()),
        ],
    };
}
