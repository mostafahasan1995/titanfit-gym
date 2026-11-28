using Gym.Cafeteria.Pages;
using Gym.Cafeteria.Receipts;
using Gym.Domain;
using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Cafeteria;

/// <summary>
/// The Cafeteria app for the shared shell: name, default station name and the sidebar pages (in order).
/// To add a page: a PageView in Pages/, its strings in CafeteriaText, one line below. The smoke screenshots pick it up.
/// Hardware: a USB barcode scanner in keyboard mode (no driver) and any Windows printer for the receipts
/// (<see cref="IReceiptPrinter"/>; ESC/POS can come once the printer model is known).
/// </summary>
internal static class CafeteriaApp
{
    private static readonly PreviewReceiptPrinter SamplePrinter = new();
    private static readonly WindowsReceiptPrinter WindowsPrinter = new();

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
            new("shift-close", UiIcons.Clock, CafeteriaText.ShiftClose, context => new ShiftClosePage(context)),
            new("receipts", UiIcons.Receipt, CafeteriaText.Receipts, context => new ReceiptsPage(context)) { Title = CafeteriaText.ReceiptsTitle },
        ],

        // Extra screenshots (sample data): a sale just paid (in dollars, then in pounds), the stock that cannot go below
        // zero, the shift report, a receipt paid in pounds, and last the exchange rate warnings (they stay on).
        SmokeStates =
        [
            new("cashier-paid", "cashier", view => ((CashierPage)view).ChargeForScreenshotAsync()),
            new("cashier-syp", "cashier", view => ((CashierPage)view).PayInSypForScreenshotAsync()),
            new("cashier-paid-syp", "cashier", view => ((CashierPage)view).ChargeInSypForScreenshotAsync()),
            new("stock-adjust", "stock", view => ((StockPage)view).OpenAdjustForScreenshotAsync()),
            new("shift-report", "shift-close", view => ((ShiftClosePage)view).OpenReportForScreenshot()),
            new("receipts-syp", "receipts", view => ((ReceiptsPage)view).ShowSypSaleForScreenshotAsync()),
            new("cashier-rate-stale", "cashier", view => ((CashierPage)view).ShowRateStateForScreenshotAsync(ExchangeRateState.Stale)),
            new("cashier-no-rate", "cashier", view => ((CashierPage)view).ShowRateStateForScreenshotAsync(ExchangeRateState.None)),
        ],
        SelfTestChecks = CafeteriaSelfTest.Checks,
    };

    /// <summary>The receipt printer: nothing is printed in the smoke screenshots (no hardware in CI).</summary>
    public static IReceiptPrinter PrinterFor(PageContext context) => context.IsSample ? SamplePrinter : WindowsPrinter;
}
