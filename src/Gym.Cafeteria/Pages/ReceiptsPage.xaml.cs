using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Gym.Cafeteria.Receipts;
using Gym.Contracts;
using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Cafeteria.Pages;

/// <summary>A sale in the receipts list.</summary>
public sealed record SaleListItem(SaleSummaryDto Sale, AppLanguage Language)
{
    public string Title => BidiText.FixNumbers(CafeteriaText.ReceiptNumbered.Format(Language, Sale.Number), Language);

    /// <summary>"18:42 · 3 items", and the pounds taken for a sale paid in pounds.</summary>
    public string Detail => BidiText.FixNumbers(
        DateText.Time(Sale.At) + " · " + (Sale.ItemCount == 1
            ? CafeteriaText.OneItem.For(Language)
            : CafeteriaText.ItemCount.Format(Language, NumberText.Format(Sale.ItemCount)))
        + (IsSyp ? " · " + Money.FormatSyp(Sale.PaidAmount, Language) : ""),
        Language);

    public bool IsSyp => Sale.Currency == Gym.Domain.Currency.Syp;

    /// <summary>US dollars.</summary>
    public decimal Total => Sale.Total;
}

/// <summary>
/// The receipts screen: the day's sales, the selected receipt as it prints (and Reprint), the receipt printer
/// setting with a test print, and a field to test the barcode scanner.
/// </summary>
public partial class ReceiptsPage : PageView
{
    private readonly IReceiptPrinter printer;
    private List<SaleListItem> sales = [];
    private ReceiptDto? shown;
    private bool syncing;

    public ReceiptsPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
        printer = CafeteriaApp.PrinterFor(context);
        syncing = true;
        DayPicker.SelectedDate = DateTime.Today;
        DayPicker.DisplayDateEnd = DateTime.Today;
        syncing = false;
        Context.Live.SaleCompleted += sale => _ = Dispatcher.InvokeAsync(async () =>
        {
            if (IsVisible && DayPicker.SelectedDate?.Date == DateTime.Today)
            {
                try
                {
                    await LoadSalesAsync(CancellationToken.None).ConfigureAwait(true);
                }
#pragma warning disable CA1031 // The next visit reloads the list.
                catch (Exception)
#pragma warning restore CA1031
                {
                }
            }
        });
    }

    /// <summary>The receipt on screen (tests, smoke screenshots).</summary>
    public ReceiptDto? Shown => shown;

    /// <summary>Smoke screenshot "receipts-syp": selects the newest sale of the day paid in Syrian pounds.</summary>
    internal async Task ShowSypSaleForScreenshotAsync()
    {
        if (sales.FirstOrDefault(s => s.IsSyp) is not { } item)
        {
            return;
        }

        syncing = true;
        SalesList.SelectedItem = item;
        syncing = false;
        shown = await Context.Api.GetSaleAsync(item.Sale.Id).ConfigureAwait(true);
        ShowReceipt();
    }

    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        FillPrinters();
        await LoadSalesAsync(cancellationToken).ConfigureAwait(true);
    }

    public override void OnLanguageChanged()
    {
        sales = sales.Select(s => s with { Language = Context.Language }).ToList();
        ShowList();
        FillPrinters();
        ShowReceipt();
        ScanTestResult.Text = "";
    }

    private async Task LoadSalesAsync(CancellationToken ct)
    {
        var day = DateOnly.FromDateTime(DayPicker.SelectedDate ?? DateTime.Today);
        var cashier = Context.Session.IsAdmin ? (int?)null : Context.Session.Staff.Id;
        var list = await Context.Api.GetSalesAsync(day, day, cashier, ct).ConfigureAwait(true);
        sales = list.OrderByDescending(s => s.At).Select(s => new SaleListItem(s, Context.Language)).ToList();
        ShowList();
        var keep = shown is { } current ? sales.FirstOrDefault(s => s.Sale.Id == current.Id) : null;
        var pick = keep ?? sales.FirstOrDefault();
        syncing = true;
        SalesList.SelectedItem = pick;
        syncing = false;
        shown = pick is null ? null : await Context.Api.GetSaleAsync(pick.Sale.Id, ct).ConfigureAwait(true);
        ShowReceipt();
    }

    private void ShowList()
    {
        var language = Context.Language;
        var selectedId = (SalesList.SelectedItem as SaleListItem)?.Sale.Id;
        syncing = true;
        SalesList.ItemsSource = sales;
        SalesList.SelectedItem = sales.FirstOrDefault(s => s.Sale.Id == selectedId);
        syncing = false;
        NoSales.Visibility = sales.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        var total = sales.Sum(s => s.Total);
        var pounds = sales.Where(s => s.IsSyp).Sum(s => s.Sale.PaidAmount);
        var line = sales.Count == 1
            ? CafeteriaText.OneSaleTotal.Format(language, Money.Format(total, language))
            : CafeteriaText.SalesTotalLine.Format(language, NumberText.Format(sales.Count), Money.Format(total, language));
        if (pounds > 0)
        {
            line += " · " + CafeteriaText.SypCollectedLine.Format(language, Money.FormatSyp(pounds, language));
        }

        DayTotalText.Text = BidiText.FixNumbers(line, language);
    }

    private void ShowReceipt()
    {
        Paper.Child = shown is null ? null : ReceiptView.Create(ReceiptLayout.ForSale(shown, Context.Language), ReceiptView.PaperWidth);
        Paper.Visibility = shown is null ? Visibility.Collapsed : Visibility.Visible;
        ReprintButton.IsEnabled = shown is not null && !Context.IsSample;
    }

    private void FillPrinters()
    {
        var language = Context.Language;
        var saved = Context.Station.PrinterName;
        IReadOnlyList<string> names = Context.IsSample ? [] : printer.GetPrinterNames();
        PrinterBox.Items.Clear();
        PrinterBox.Items.Add(new ComboBoxItem { Content = CafeteriaText.DefaultPrinter.For(language), Tag = null });
        foreach (var name in names)
        {
            PrinterBox.Items.Add(new ComboBoxItem { Content = name, Tag = name, FlowDirection = FlowDirection.LeftToRight });
        }

        var missing = !string.IsNullOrWhiteSpace(saved) && !names.Contains(saved, StringComparer.OrdinalIgnoreCase);
        if (missing)
        {
            PrinterBox.Items.Add(new ComboBoxItem { Content = saved, Tag = saved, FlowDirection = FlowDirection.LeftToRight });
        }

        PrinterBox.SelectedItem = PrinterBox.Items.OfType<ComboBoxItem>()
            .FirstOrDefault(i => string.Equals(i.Tag as string, string.IsNullOrWhiteSpace(saved) ? null : saved, StringComparison.OrdinalIgnoreCase))
            ?? PrinterBox.Items[0];
        PrinterNote.Text = Context.IsSample
            ? CafeteriaText.SamplePrinterNote.For(language)
            : missing
                ? CafeteriaText.SavedPrinterMissing.For(language)
                : CafeteriaText.PrinterNote.For(language);
        TestPrintButton.IsEnabled = !Context.IsSample;
    }

    private string? SelectedPrinter => (PrinterBox.SelectedItem as ComboBoxItem)?.Tag as string;

    private void Print(ReceiptLayout layout, string? printerName)
    {
        try
        {
            printer.Print(layout, printerName);
            Context.Ui.Toast(CafeteriaText.ReceiptPrinted);
        }
        catch (ReceiptPrintException ex)
        {
            Context.Ui.Toast(
                CafeteriaText.PrintFailedDetail.Format(Context.Language, ex.Text.For(Context.Language) + (ex.Detail is { Length: > 0 } detail ? " " + detail : "")),
                ToastKind.Error);
        }
    }

    private async void OnDayChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (syncing || !IsLoaded)
        {
            return;
        }

        try
        {
            shown = null;
            await LoadSalesAsync(CancellationToken.None).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Show it; the screen stays usable.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Context.Ui.ShowError(ex);
        }
    }

    private async void OnSaleSelected(object sender, SelectionChangedEventArgs e)
    {
        if (syncing || SalesList.SelectedItem is not SaleListItem item || item.Sale.Id == shown?.Id)
        {
            return;
        }

        try
        {
            shown = await Context.Api.GetSaleAsync(item.Sale.Id).ConfigureAwait(true);
            ShowReceipt();
        }
#pragma warning disable CA1031 // Show it; the list stays usable.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Context.Ui.ShowError(ex);
        }
    }

    private void OnReprintClick(object sender, RoutedEventArgs e)
    {
        if (shown is not null && !Context.IsSample)
        {
            Print(ReceiptLayout.ForSale(shown, Context.Language), Context.Station.PrinterName);
        }
    }

    private void OnSavePrinterClick(object sender, RoutedEventArgs e)
    {
        var name = SelectedPrinter;
        var saved = Context.SaveStation(s => s with { PrinterName = name });
        Context.Ui.Toast(saved ? CafeteriaText.PrinterSaved : CafeteriaText.PrinterSaveFailed, saved ? ToastKind.Success : ToastKind.Error);
        FillPrinters();
    }

    private void OnTestPrintClick(object sender, RoutedEventArgs e)
    {
        if (Context.IsSample)
        {
            return;
        }

        var name = SelectedPrinter;
        Print(ReceiptLayout.TestPage(name ?? CafeteriaText.DefaultPrinter.For(Context.Language), Context.StationName, Context.Language, DateTime.UtcNow), name);
    }

    /// <summary>The scanner types the code and Enter into this box: show what was read.</summary>
    private async void OnScanTestKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        e.Handled = true;
        var code = ScanTestInput.Text.Trim();
        ScanTestInput.Text = "";
        if (code.Length == 0)
        {
            return;
        }

        try
        {
            var product = await Context.Api.FindProductByBarcodeAsync(code).ConfigureAwait(true);
            ScanTestResult.Text = product is null
                ? CafeteriaText.ScanUnknown.Format(Context.Language, code)
                : CafeteriaText.ScanFound.Format(Context.Language, code, ReceiptLayout.ProductName(product.NameEn, product.NameAr, Context.Language));
        }
#pragma warning disable CA1031 // Show it; the test field stays usable.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ScanTestResult.Text = ErrorText.Describe(ex, Context.Language);
        }
    }
}
