using Gym.Admin.Pages;
using Gym.Domain;
using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Admin;

/// <summary>
/// The Admin app for the shared shell: name, default station name and the sidebar pages (in order).
/// To add a page: a PageView in Pages/, its strings in AdminText, one line below. The smoke screenshots pick it up.
/// </summary>
internal static class AdminApp
{
    public static AppDefinition Definition { get; } = new()
    {
        App = GymApp.Admin,
        AppName = AdminText.AppName,
        NavLabel = AdminText.NavLabel,
        DefaultStationName = "Admin PC",
        Pages =
        [
            new("dashboard", UiIcons.Dashboard, AdminText.Dashboard, context => new DashboardPage(context)),
            new("exchange-rate", UiIcons.Cash, AdminText.ExchangeRate, context => new ExchangeRatePage(context)),
            new("members", UiIcons.Users, AdminText.Members, context => new MembersPage(context)),
            new("plans", UiIcons.Tag, AdminText.Plans, context => new PlansPage(context)),
            new("cafeteria-items", UiIcons.Box, AdminText.CafeteriaItems, context => new CafeteriaItemsPage(context)),
            new("staff", UiIcons.Staff, AdminText.Staff, context => new StaffPage(context)),
            new("settings", UiIcons.Settings, AdminText.Settings, context => new SettingsPage(context)),
            new("reports", UiIcons.Reports, AdminText.Reports, context => new ReportsPage(context)),
            new("backups", UiIcons.Database, AdminText.Backups, context => new BackupsPage(context)),
        ],

        // Smoke screenshots only: the dialogs and the warning states, so CI shows them too.
        SmokeStates =
        [
            new("members-edit", "members", view => Open(() => ((MembersPage)view).EditForSmoke())),
            new("cafeteria-items-stock", "cafeteria-items", view => Open(() => ((CafeteriaItemsPage)view).StockForSmoke())),
            new("staff-edit", "staff", view => Open(() => ((StaffPage)view).EditSelectedForSmoke())),
            new("staff-rating", "staff", view => Open(() => ((StaffPage)view).RateSelectedForSmoke())),
            new("reports-attendance", "reports", view => ((ReportsPage)view).ShowForSmokeAsync(sales: false)),
            new("reports-sales", "reports", view => ((ReportsPage)view).ShowForSmokeAsync(sales: true)),
            new("dashboard-backup-warning", "dashboard", view => ((DashboardPage)view).ShowBackupFailureForSmokeAsync()),
            new("backups-failed", "backups", _ => Task.CompletedTask),

            // Last: they change the sample's exchange rate (the banner stays for the shots after them).
            new("exchange-rate-stale", "exchange-rate", view => ((ExchangeRatePage)view).ShowStateForSmokeAsync(ExchangeRateState.Stale)),
            new("exchange-rate-none", "exchange-rate", view => ((ExchangeRatePage)view).ShowStateForSmokeAsync(ExchangeRateState.None)),
        ],
        SelfTestChecks = AdminSelfTest.Checks,
    };

    private static Task Open(Action open)
    {
        open();
        return Task.CompletedTask;
    }
}
