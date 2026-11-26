using Gym.Admin.Pages;
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
            new("members", UiIcons.Users, AdminText.Members, context => new MembersPage(context)),
            new("plans", UiIcons.Tag, AdminText.Plans, context => new PlansPage(context)),
            new("cafeteria-items", UiIcons.Box, AdminText.CafeteriaItems, context => new CafeteriaItemsPage(context)),
            new("staff", UiIcons.Staff, AdminText.Staff, context => new StaffPage(context)),
            new("settings", UiIcons.Settings, AdminText.Settings, context => new SettingsPage(context)),
            new("reports", UiIcons.Reports, AdminText.Reports, context => new ReportsPage(context)),
            new("backups", UiIcons.Database, AdminText.Backups, context => new BackupsPage(context)),
        ],
    };
}
