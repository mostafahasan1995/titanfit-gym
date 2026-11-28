using System.Windows;
using Gym.Client.Sample;
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

        // Touch screen, check-in read from a distance: drawn larger on big screens than the other apps.
        ScaleReference = UiScale.TouchReference,
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
            new("new-member-filled-syp", "new-member", view => ((NewMemberPage)view).FillSampleSypAsync()),
            new("new-member-filled", "new-member", view => ((NewMemberPage)view).FillSampleAsync()),
            new("new-member-saved", "new-member", view => ((NewMemberPage)view).SaveSampleAsync()),
            new("members-renew", "members", view => ((MembersPage)view).ShowRenewSampleAsync()),
            new("members-renew-syp", "members", view => ((MembersPage)view).ShowRenewSampleAsync(Currency.Syp)),
            new("members-paid-syp", "members", view => ((MembersPage)view).ShowSypPaymentSampleAsync()),
            new("members-qr", "members", view => ((MembersPage)view).ShowQrSampleAsync()),
            new("private-trainers-pay", "private-trainers", view => ((PrivateTrainersPage)view).ShowPaySampleAsync()),
            new("private-trainers-pay-syp", "private-trainers", view => ((PrivateTrainersPage)view).ShowPaySampleAsync(Currency.Syp)),

            // Last: the exchange rate warnings stay on screen for the shots after them.
            new("rate-stale", "check-in", view => SimulateRateAsync(view, ExchangeRateState.Stale)),
            new("rate-missing", "members", view => SimulateRateAsync(view, ExchangeRateState.None)),
        ],
        SelfTestChecks = ReceptionSelfTest.Checks,
    };

    /// <summary>
    /// Smoke screenshots: the sample's exchange rate becomes an older day's (the warning banner, the last rate still
    /// used) or none at all (pounds hidden, dollars only). The shared rate is reloaded at once.
    /// </summary>
    private static async Task SimulateRateAsync(FrameworkElement view, ExchangeRateState state)
    {
        if (view is PageView { Context.Api: SampleGymApi sample })
        {
            sample.SimulateExchangeRate(state);
            ExchangeRates.Current.Set(await sample.GetExchangeRateAsync().ConfigureAwait(true));
        }
    }
}
