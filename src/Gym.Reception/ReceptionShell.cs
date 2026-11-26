using Gym.UI;
using Gym.UI.Controls;

namespace Gym.Reception;

/// <summary>Sidebar and strings from design/Main.dc.html (Reception · Check-in).</summary>
internal static class ReceptionShell
{
    public static ShellDefinition Definition { get; } = new(
        AppName: new("Reception", "الاستقبال"),
        NavLabel: new("Reception menu", "قائمة الاستقبال"),
        Station: new("Reception 1", "الاستقبال 1"),
        Role: new("Receptionist", "موظف استقبال"),
        Pages:
        [
            new(UiIcons.Scan, new("Check-in", "تسجيل الحضور")),
            new(UiIcons.UserPlus, new("New member", "مشترك جديد")),
            new(UiIcons.Users, new("Members", "المشتركون")),
            new(UiIcons.Renew, new("Renewals", "التجديدات")),
        ]);
}
