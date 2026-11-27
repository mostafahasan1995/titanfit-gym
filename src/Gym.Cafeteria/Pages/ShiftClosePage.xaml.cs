using System.Windows;
using System.Windows.Controls;
using Gym.Cafeteria.Receipts;
using Gym.Cafeteria.Sales;
using Gym.Client;
using Gym.Contracts;
using Gym.UI;
using Gym.UI.Controls;
using Gym.UI.Shell;

namespace Gym.Cafeteria.Pages;

/// <summary>The shift-close screen. Logic in <see cref="ShiftViewModel"/>; this class wires the controls and the printer.</summary>
public partial class ShiftClosePage : PageView
{
    private readonly ShiftViewModel model;
    private readonly IReceiptPrinter printer;
    private bool loaded;
    private bool syncing;

    public ShiftClosePage(PageContext context)
        : base(context)
    {
        InitializeComponent();
        printer = CafeteriaApp.PrinterFor(context);
        model = new ShiftViewModel(context.Api, () => Context.StationName, context.Session.IsAdmin ? null : context.Session.Staff.Id, context.Language);
        HistoryList.ItemsSource = model.History;
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ShiftViewModel.Shown) or nameof(ShiftViewModel.Current))
            {
                UpdateView();
            }
        };
        Context.Live.SaleCompleted += sale => _ = Dispatcher.InvokeAsync(async () =>
        {
            if (loaded && IsVisible)
            {
                await QuietlyAsync(() => model.LoadAsync()).ConfigureAwait(true);
            }
        });
    }

    public ShiftViewModel Model => model;

    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        await model.LoadAsync(cancellationToken).ConfigureAwait(true);
        loaded = true;
        UpdateView();
    }

    public override void OnLanguageChanged()
    {
        model.Language = Context.Language;
        UpdateView();
    }

    private void UpdateView()
    {
        var language = Context.Language;
        var shown = model.Shown;
        SummaryPanel.Visibility = shown is null ? Visibility.Collapsed : Visibility.Visible;
        NoShift.Visibility = shown is null && loaded ? Visibility.Visible : Visibility.Collapsed;
        CloseCard.Visibility = model.Current is null ? Visibility.Collapsed : Visibility.Visible;
        OpenCard.Visibility = model.Current is null && loaded ? Visibility.Visible : Visibility.Collapsed;
        syncing = true;
        HistoryList.SelectedItem = shown is null ? null : model.History.FirstOrDefault(h => h.Id == shown.Shift.Id);
        syncing = false;
        UpdateDifference();
        if (shown is null)
        {
            return;
        }

        var shift = shown.Shift;
        var isOpen = shift.ClosedAt is null;
        ShiftTitle.Text = BidiText.FixNumbers(CafeteriaText.ShiftTitle.Format(language, NumberText.Format(shift.Id)), language);
        ShiftStatus.Tone = isOpen ? PillTone.Good : PillTone.Neutral;
        ShiftStatus.Text = (isOpen ? CafeteriaText.ShiftStillOpen : CafeteriaText.ShiftClosedState).For(language);
        BidiText.SetText(ShiftStation, shift.Station);
        BidiText.SetText(ShiftCashier, shift.CashierName);
        ShiftWhen.Text = BidiText.FixNumbers(
            shift.ClosedAt is { } closed
                ? CafeteriaText.OpenedClosed.Format(language, DateText.DateAndTime(shift.OpenedAt, language), DateText.Time(closed))
                : CafeteriaText.OpenedAt.Format(language, DateText.DateAndTime(shift.OpenedAt, language)),
            language);
        SalesCountText.Text = NumberText.Format(shown.SaleCount);
        SalesTotalText.Amount = shown.SalesTotal;
        OpeningCashText.Amount = shift.OpeningCash;
        ExpectedCashText.Amount = shown.ExpectedCash;

        CountedBox.Visibility = !isOpen ? Visibility.Visible : Visibility.Collapsed;
        if (shift.ClosingCash is { } counted)
        {
            CountedText.Text = BidiText.FixNumbers(CafeteriaText.CountedWas.Format(language, Money.Format(counted, language)), language);
            ShowDifference(CountedDifferenceText, shown.CashDifference ?? 0);
        }
        else
        {
            CountedText.Text = CafeteriaText.NotCounted.For(language);
            CountedDifferenceText.Visibility = Visibility.Collapsed;
        }

        var items = shown.Products.Select(p => new SoldItem(p, language)).ToList();
        ItemsGrid.ItemsSource = items;
        NoSales.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>Counted minus expected, while typing the counted cash.</summary>
    private void UpdateDifference()
    {
        var current = model.Current;
        var counted = Money.Parse(CountedInput.Text);
        var typed = CountedInput.Text.Trim().Length > 0;
        CountedField.Error = typed && counted is null or < 0 ? CafeteriaText.InvalidAmount.For(Context.Language) : null;
        if (current is null || counted is not { } cash || cash < 0)
        {
            DifferenceRow.Visibility = Visibility.Collapsed;
            return;
        }

        var difference = ShiftViewModel.Difference(current.ExpectedCash, cash) ?? 0;
        DifferenceRow.Visibility = Visibility.Visible;
        DifferenceLabel.Text = (difference < 0 ? CafeteriaText.CashMissing : difference > 0 ? CafeteriaText.CashExtra : CafeteriaText.CashMatches).For(Context.Language);
        ShowDifference(DifferenceText, difference);
    }

    private static void ShowDifference(MoneyText text, long difference)
    {
        text.Visibility = Visibility.Visible;
        text.Amount = difference;
        text.SetResourceReference(TextBlock.ForegroundProperty, difference < 0 ? "Brush.Bad" : difference > 0 ? "Brush.Warning" : "Brush.Good");
    }

    private async Task QuietlyAsync(Func<Task> work)
    {
        try
        {
            await work().ConfigureAwait(true);
        }
#pragma warning disable CA1031 // A background refresh that fails is caught up on the next visit.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    private void Print(ShiftSummaryDto summary)
    {
        if (Context.IsSample)
        {
            return;
        }

        try
        {
            printer.Print(ReceiptLayout.ForShift(summary, Context.Language, DateTime.UtcNow), Context.Station.PrinterName);
            Context.Ui.Toast(CafeteriaText.ZReportPrinted);
        }
        catch (ReceiptPrintException ex)
        {
            Context.Ui.Toast(CafeteriaText.PrintFailedDetail.Format(Context.Language, ex.Text.For(Context.Language) + (ex.Detail is { Length: > 0 } detail ? " " + detail : "")),
                ToastKind.Error);
        }
    }

    private void OnCountedChanged(object sender, TextChangedEventArgs e) => UpdateDifference();

    private async void OnCloseClick(object sender, RoutedEventArgs e)
    {
        if (model.Current is not { } current)
        {
            return;
        }

        var language = Context.Language;
        long? counted = null;
        if (CountedInput.Text.Trim().Length > 0)
        {
            counted = Money.Parse(CountedInput.Text);
            if (counted is null or < 0)
            {
                UpdateDifference();
                CountedInput.Focus();
                return;
            }
        }

        var message = BidiText.FixNumbers(
            counted is { } cash
                ? CafeteriaText.CloseShiftConfirmCounted.Format(language, NumberText.Format(current.SaleCount), Money.Format(current.SalesTotal, language), Money.Format(cash, language))
                : CafeteriaText.CloseShiftConfirm.Format(language, NumberText.Format(current.SaleCount), Money.Format(current.SalesTotal, language)),
            language);
        if (!await Context.Ui.ConfirmAsync(CafeteriaText.CloseShiftTitle, new LocalText(message, message), CafeteriaText.CloseShift).ConfigureAwait(true))
        {
            return;
        }

        try
        {
            var closed = await Context.Ui.RunBusyAsync(() => model.CloseAsync(counted, NoteInput.Text)).ConfigureAwait(true);
            CountedInput.Text = "";
            NoteInput.Text = "";
            Context.Ui.Toast(CafeteriaText.ShiftClosedToast);
            Print(closed);
        }
        catch (GymApiException ex)
        {
            Context.Ui.ShowError(ex);
            await QuietlyAsync(() => model.LoadAsync()).ConfigureAwait(true);
        }
    }

    private async void OnOpenClick(object sender, RoutedEventArgs e)
    {
        var opening = Money.Parse(OpeningInput.Text) ?? (OpeningInput.Text.Trim().Length == 0 ? 0 : -1);
        if (opening < 0)
        {
            OpeningField.Error = CafeteriaText.InvalidAmount.For(Context.Language);
            return;
        }

        OpeningField.Error = null;
        try
        {
            await Context.Ui.RunBusyAsync(() => model.OpenAsync(opening)).ConfigureAwait(true);
            Context.Ui.Toast(CafeteriaText.ShiftOpenedToast);
        }
        catch (GymApiException ex)
        {
            Context.Ui.ShowError(ex);
        }
    }

    private async void OnPrintClick(object sender, RoutedEventArgs e) => await ShowReportAsync().ConfigureAwait(true);

    /// <summary>Smoke screenshot "shift-report": opens the report preview (the screenshot closes it).</summary>
    internal Task OpenReportForScreenshot()
    {
        _ = ShowReportAsync();
        return Task.CompletedTask;
    }

    /// <summary>The shift (Z) report of the shown shift as it prints, with a Print button.</summary>
    private async Task ShowReportAsync()
    {
        if (model.Shown is not { } summary)
        {
            return;
        }

        var paper = new Border
        {
            Background = System.Windows.Media.Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 4, 0, 12),
            Child = ReceiptView.Create(ReceiptLayout.ForShift(summary, Context.Language, DateTime.UtcNow), ReceiptView.PaperWidth),
            Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 16, ShadowDepth = 3, Opacity = 0.45 },
        };
        var scroller = new ScrollViewer
        {
            Content = paper,
            MaxHeight = 560,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Focusable = false,
        };
        await Context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = CafeteriaText.ZReportTitle,
            Message = Context.IsSample ? CafeteriaText.SamplePrinterNote : null,
            Content = scroller,
            PrimaryText = CafeteriaText.Print,
            SecondaryText = CommonText.Close,
            Width = 460,
            OnPrimary = () =>
            {
                Print(summary);
                return Task.FromResult(true);
            },
        }).ConfigureAwait(true);
    }

    private async void OnHistorySelected(object sender, SelectionChangedEventArgs e)
    {
        if (syncing || HistoryList.SelectedItem is not ShiftListItem item || item.Id == model.Shown?.Shift.Id)
        {
            return;
        }

        try
        {
            await model.ShowAsync(item.Id).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Show it; the list stays usable.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Context.Ui.ShowError(ex);
        }
    }
}
