using Gym.Cafeteria.Pages;
using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Cafeteria;

/// <summary>
/// The Cafeteria app for the shared shell: name, default station name and the sidebar pages (in order).
/// To add a page: a PageView in Pages/, its strings in CafeteriaText, one line below. The smoke screenshots pick it up.
/// </summary>
internal static class CafeteriaApp
{
    public static AppDefinition Definition { get; } = new()
    {
        App = GymApp.Cafeteria,
        AppName = CafeteriaText.AppName,
        NavLabel = CafeteriaText.NavLabel,
        DefaultStationName = "Cafeteria 1",
        Pages =
        [
            new("cashier", UiIcons.Cart, CafeteriaText.Cashier, context => new CashierPage(context)) { Title = CafeteriaText.CashierTitle },
            new("stock", UiIcons.Box, CafeteriaText.Stock, context => new StockPage(context)),
            new("shift-close", UiIcons.Receipt, CafeteriaText.ShiftClose, context => new ShiftClosePage(context)),
        ],
    };
}
