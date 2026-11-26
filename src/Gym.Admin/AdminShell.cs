using Gym.UI;
using Gym.UI.Controls;

namespace Gym.Admin;

/// <summary>Sidebar and strings from design/Admin-Dashboard.dc.html (Admin · Dashboard).</summary>
internal static class AdminShell
{
    public static ShellDefinition Definition { get; } = new(
        AppName: new("Admin", "الإدارة"),
        NavLabel: new("Admin menu", "قائمة الإدارة"),
        Station: new("Admin PC · server", "جهاز الإدارة · الخادم"),
        Role: new("Administrator", "مسؤول النظام"),
        Pages:
        [
            new(UiIcons.Dashboard, new("Dashboard", "لوحة التحكم")),
            new(UiIcons.Users, new("Members", "المشتركون")),
            new(UiIcons.Tag, new("Plans & prices", "الخطط والأسعار")),
            new(UiIcons.Box, new("Cafeteria items", "أصناف الكافتيريا")),
            new(UiIcons.Staff, new("Staff", "الموظفون")),
            new(UiIcons.Reports, new("Reports", "التقارير")),
            new(UiIcons.Settings, new("Settings", "الإعدادات")),
        ]);
}
