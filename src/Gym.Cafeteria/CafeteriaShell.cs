using Gym.UI;
using Gym.UI.Controls;

namespace Gym.Cafeteria;

/// <summary>Sidebar and strings from design/Cafeteria-POS.dc.html (Cafeteria · Cashier).</summary>
internal static class CafeteriaShell
{
    public static ShellDefinition Definition { get; } = new(
        AppName: new("Cafeteria", "الكافتيريا"),
        NavLabel: new("Cafeteria menu", "قائمة الكافتيريا"),
        Station: new("Cafeteria 1", "الكافتيريا 1"),
        Role: new("Cashier", "كاشير"),
        Pages:
        [
            new(UiIcons.Cart, new("Sell", "البيع"), Title: new LocalText("Cashier", "نقطة البيع")),
            new(UiIcons.Box, new("Stock", "المخزون")),
            new(UiIcons.Receipt, new("Shift report", "تقرير الوردية")),
        ]);
}
