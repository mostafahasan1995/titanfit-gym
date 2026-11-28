using System.ComponentModel;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;
using Gym.Cafeteria.Hardware;
using Gym.Cafeteria.Receipts;
using Gym.Cafeteria.Sales;
using Gym.Contracts;
using Gym.Domain;
using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Cafeteria.Pages;

/// <summary>One item on the low-stock banner.</summary>
public sealed record LowStockChip(string Name, string Stock);

/// <summary>
/// The cashier (POS) screen. The logic is in <see cref="CashierViewModel"/>; this class wires the controls, the
/// barcode scanner (keyboard wedge on the window), live stock updates and the receipt printer.
/// </summary>
public partial class CashierPage : PageView
{
    public static readonly DependencyProperty TileColumnsProperty = DependencyProperty.Register(
        nameof(TileColumns), typeof(int), typeof(CashierPage), new FrameworkPropertyMetadata(4));

    private const double MinTileWidth = 172;
    private const int BannerItems = 3;

    private readonly CashierViewModel model;
    private readonly BarcodeWedge wedge = new();
    private readonly IReceiptPrinter printer;
    private (TextBox Box, string Text, int Caret)? beforeScan;
    private Window? window;
    private bool loaded;
    private ReceiptDto? paid;
    private LocalText? printStatus;
    private string? printError;

    public CashierPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
        printer = CafeteriaApp.PrinterFor(context);
        model = new CashierViewModel(context.Api, () => Context.StationName, context.Language);
        TileList.ItemsSource = model.Tiles;
        CategoryList.ItemsSource = model.Categories;
        LineList.ItemsSource = model.Cart.Lines;
        model.Cart.PropertyChanged += (_, _) => UpdateOrder();
        model.Tiles.CollectionChanged += (_, _) => NoTiles.Visibility = loaded && model.Tiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CashierViewModel.Shift))
            {
                UpdateShift();
            }
            else if (e.PropertyName == nameof(CashierViewModel.LowStock))
            {
                UpdateLowStock();
            }
        };

        Context.Live.StockChanged += change => _ = Dispatcher.InvokeAsync(() =>
        {
            if (loaded)
            {
                model.ApplyStock(change.Products);
            }
        });
        Context.Live.ProductChanged += change => _ = Dispatcher.InvokeAsync(() => ReloadProductAsync(change.ProductId));
        Context.Live.SaleCompleted += sale => _ = Dispatcher.InvokeAsync(() => RefreshShiftQuietlyAsync());

        // A new rate (or none any more) changes the amount in pounds and the quick amounts.
        PropertyChangedEventManager.AddHandler(model.Rates, (_, _) => UpdateOrder(), string.Empty);

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        UpdateOrder();
        UpdateShift();
    }

    /// <summary>Tile columns for the width of the grid.</summary>
    public int TileColumns
    {
        get => (int)GetValue(TileColumnsProperty);
        set => SetValue(TileColumnsProperty, value);
    }

    /// <summary>For the tests and the smoke screenshots.</summary>
    public CashierViewModel Model => model;

    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        var first = !loaded;
        await model.LoadAsync(cancellationToken).ConfigureAwait(true);
        loaded = true;
        NoTiles.Visibility = model.Tiles.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (first && Context.IsSample)
        {
            FillSampleCart();
        }

        UpdateAll();
        if (!Context.IsSample)
        {
            SearchInput.Focus();
        }
    }

    public override void OnLanguageChanged()
    {
        model.Language = Context.Language;
        UpdateAll();
    }

    /// <summary>Smoke screenshot "cashier-paid": sells the sample cart, so the paid panel shows.</summary>
    internal Task ChargeForScreenshotAsync() => ChargeAsync();

    /// <summary>Smoke screenshot "cashier-syp": the sample cart paid in Syrian pounds, the pounds typed in, the change shown.</summary>
    internal Task PayInSypForScreenshotAsync()
    {
        if (paid is not null)
        {
            StartNewOrder();
        }

        if (model.Cart.IsEmpty)
        {
            FillSampleCart();
        }

        CurrencyInput.Currency = Currency.Syp;
        var due = model.AmountDue.Amount;
        CashInput.Text = Money.FormatSypAmount(CashMath.RoundUp(due, 50_000));
        UpdateOrder();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Smoke screenshots of the exchange rate warning: the sample's rate becomes yesterday's (stale) or none at all,
    /// then the cart is filled again in dollars. The shell shows the banner from the live update.
    /// </summary>
    internal async Task ShowRateStateForScreenshotAsync(ExchangeRateState state)
    {
        if (Context.Api is Gym.Client.Sample.SampleGymApi sample)
        {
            sample.SimulateExchangeRate(state);
        }

        await Dispatcher.Yield(DispatcherPriority.Background);
        if (paid is not null)
        {
            StartNewOrder();
        }

        CurrencyInput.Currency = Currency.Usd;
        if (model.Cart.IsEmpty)
        {
            FillSampleCart();
        }

        UpdateOrder();
    }

    /// <summary>Smoke screenshot "cashier-paid-syp": sells the sample cart in Syrian pounds (paid panel with the change in pounds).</summary>
    internal async Task ChargeInSypForScreenshotAsync()
    {
        await PayInSypForScreenshotAsync().ConfigureAwait(true);
        await ChargeAsync().ConfigureAwait(true);
    }

    /// <summary>The smoke screenshots show a filled cart with the cash typed in (sample data only).</summary>
    private void FillSampleCart()
    {
        foreach (var id in new[] { 6, 1, 1, 9 })
        {
            if (model.Products.FirstOrDefault(p => p.Id == id) is { } product)
            {
                model.Add(product);
            }
        }

        if (!model.Cart.IsEmpty)
        {
            CashInput.Text = Money.FormatAmount(CashMath.RoundUp(model.Cart.Total, 10));
        }
    }

    private void UpdateAll()
    {
        UpdateOrder();
        UpdateShift();
        UpdateLowStock();
        if (paid is not null)
        {
            ShowPaid(paid);
        }
    }

    private void UpdateOrder()
    {
        var language = Context.Language;
        var cart = model.Cart;
        ItemCountText.Text = cart.ItemCount == 1
            ? CafeteriaText.OneItem.For(language)
            : BidiText.FixNumbers(CafeteriaText.ItemCount.Format(language, NumberText.Format(cart.ItemCount)), language);
        var due = model.AmountDue;
        TotalText.Amount = cart.Total;
        TotalSypText.Amount = cart.Total;
        DueSypText.Text = BidiText.FixNumbers(CafeteriaText.DueInSyp.Format(language, Money.FormatSyp(due.Amount, language)), language);
        DueSypText.Visibility = due.Currency == Currency.Syp && !cart.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        ChargeButton.Content = BidiText.FixNumbers(CafeteriaText.ChargeAmount.Format(language, Money.Format(due.Currency, due.Amount, language)), language);
        ThemeAssist.SetPlaceholder(CashInput, (due.Currency == Currency.Syp ? CafeteriaText.CashPlaceholderSyp : CafeteriaText.CashPlaceholder).For(language));
        ChargeButton.IsEnabled = !cart.IsEmpty;
        ClearButton.IsEnabled = !cart.IsEmpty;
        var showPaid = paid is not null && cart.IsEmpty;
        ItemCountText.Visibility = showPaid ? Visibility.Collapsed : Visibility.Visible;
        PaidPanel.Visibility = showPaid ? Visibility.Visible : Visibility.Collapsed;
        OrderFooter.Visibility = showPaid ? Visibility.Collapsed : Visibility.Visible;
        LinesScroller.Visibility = showPaid ? Visibility.Collapsed : Visibility.Visible;
        EmptyCart.Visibility = !showPaid && cart.IsEmpty ? Visibility.Visible : Visibility.Collapsed;
        QuickCashList.ItemsSource = CashMath.QuickAmounts(due.Amount, due.Currency).Select(a => new QuickCash(a, due.Currency)).ToList();
        UpdateChange();
    }

    private void UpdateChange()
    {
        var due = model.AmountDue;
        var cash = Money.Parse(due.Currency, CashInput.Text);
        var typed = CashInput.Text.Trim().Length > 0;
        var tooLow = typed && (cash is null || cash < due.Amount) && due.Amount > 0;
        var change = CashMath.ChangeDue(due.Amount, cash);
        ThemeAssist.SetHasError(CashInput, tooLow);
        CashErrorText.Text = (cash is null
            ? due.Currency == Currency.Syp ? CafeteriaText.InvalidSypAmount : CafeteriaText.InvalidAmount
            : CafeteriaText.CashTooLow).For(Context.Language);
        CashErrorText.Visibility = tooLow ? Visibility.Visible : Visibility.Collapsed;
        ChangeText.Visibility = !tooLow && change is not null && due.Amount > 0 ? Visibility.Visible : Visibility.Collapsed;
        ChangeText.Currency = due.Currency;
        ChangeText.Amount = change ?? 0;
        ChangeLabel.Opacity = change is not null || tooLow ? 1 : 0.5;
    }

    private void UpdateShift()
    {
        var language = Context.Language;
        if (model.Shift is { } summary)
        {
            ShiftTitleText.Text = BidiText.FixNumbers(CafeteriaText.ShiftOpenSince.Format(language, DateText.Time(summary.Shift.OpenedAt)), language);
            ShiftDetailText.Text = summary.SaleCount == 1
                ? CafeteriaText.OneSale.For(language)
                : BidiText.FixNumbers(CafeteriaText.SaleCount.Format(language, NumberText.Format(summary.SaleCount)), language);
            ShiftTotalText.Amount = summary.SalesTotal;
            ShiftTotalText.Visibility = Visibility.Visible;
        }
        else
        {
            ShiftTitleText.Text = CafeteriaText.NoOpenShift.For(language);
            ShiftDetailText.Text = CafeteriaText.FirstSaleOpensShift.For(language);
            ShiftTotalText.Visibility = Visibility.Collapsed;
        }
    }

    private void UpdateLowStock()
    {
        var language = Context.Language;
        var low = model.LowStock;
        LowStockBanner.Visibility = low.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        LowStockTitle.Text = low.Count == 1
            ? CafeteriaText.OneItemLow.For(language)
            : BidiText.FixNumbers(CafeteriaText.ItemsLow.Format(language, NumberText.Format(low.Count)), language);
        LowStockList.ItemsSource = low.Take(BannerItems)
            .Select(p => new LowStockChip(ReceiptLayout.ProductName(p.NameEn, p.NameAr, language), NumberText.Format(p.Stock)))
            .ToList();
        LowStockMore.Text = low.Count > BannerItems
            ? BidiText.FixNumbers(CafeteriaText.MoreItems.Format(language, NumberText.Format(low.Count - BannerItems)), language)
            : "";
    }

    private void ShowPaid(ReceiptDto receipt)
    {
        var language = Context.Language;
        var paidText = receipt.Currency == Currency.Syp
            ? Money.FormatWithSyp(receipt.Total, ReceiptLayout.AmountDue(receipt), language)
            : Money.Format(receipt.Total, language);
        PaidDetailText.Text = BidiText.FixNumbers(CafeteriaText.PaidDetail.Format(language, receipt.Number, paidText), language);
        PaidChangePanel.Visibility = receipt.Change is > 0 ? Visibility.Visible : Visibility.Collapsed;
        PaidChangeText.Currency = receipt.Currency;
        PaidChangeText.Amount = receipt.Change ?? 0;
        PrintStatusText.Text = printError is { } error
            ? CafeteriaText.PrintFailedDetail.Format(language, error)
            : (printStatus ?? CafeteriaText.Printing).For(language);
        PrintStatusText.Foreground = (System.Windows.Media.Brush)FindResource(printError is null ? "Brush.TextSecondary" : "Brush.Bad");
        PrintIcon.Foreground = PrintStatusText.Foreground;
        ReprintButton.IsEnabled = !Context.IsSample;
    }

    private void StartNewOrder()
    {
        paid = null;
        printError = null;
        printStatus = null;
        CashInput.Text = "";
        UpdateOrder();
    }

    private void AddProduct(ProductDto product)
    {
        if (paid is not null)
        {
            StartNewOrder();
        }

        var language = Context.Language;
        var name = ReceiptLayout.ProductName(product.NameEn, product.NameAr, language);
        switch (model.Add(product))
        {
            case AddResult.OutOfStock:
                Warn(CafeteriaText.ItemOutOfStock.Format(language, name));
                break;
            case AddResult.NoMoreStock:
                Warn(CafeteriaText.NoMoreInStock.Format(language, name, NumberText.Format(product.Stock)));
                break;
            case AddResult.Inactive:
                Warn(CafeteriaText.ItemNotForSale.Format(language, name));
                break;
        }
    }

    private void Warn(string message)
    {
        if (!Context.IsSample)
        {
            SystemSounds.Exclamation.Play();
        }

        Context.Ui.Toast(message, ToastKind.Warning);
    }

    private async Task HandleScanAsync(string code)
    {
        try
        {
            var outcome = await model.ScanAsync(code).ConfigureAwait(true);
            if (outcome.Product is null)
            {
                Warn(CafeteriaText.UnknownBarcode.Format(Context.Language, code));
                return;
            }

            if (paid is not null)
            {
                // The scan was counted on the old order's view model state; show the new order.
                paid = null;
                printError = null;
                printStatus = null;
                CashInput.Text = "";
            }

            var name = ReceiptLayout.ProductName(outcome.Product.NameEn, outcome.Product.NameAr, Context.Language);
            switch (outcome.Result)
            {
                case AddResult.OutOfStock:
                    Warn(CafeteriaText.ItemOutOfStock.Format(Context.Language, name));
                    break;
                case AddResult.NoMoreStock:
                    Warn(CafeteriaText.NoMoreInStock.Format(Context.Language, name, NumberText.Format(outcome.Product.Stock)));
                    break;
                case AddResult.Inactive:
                    Warn(CafeteriaText.ItemNotForSale.Format(Context.Language, name));
                    break;
            }

            UpdateOrder();
        }
#pragma warning disable CA1031 // A failed lookup must not stop the till: show it and go on.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Context.Ui.ShowError(ex);
        }
    }

    private async Task ChargeAsync()
    {
        var due = model.AmountDue;
        decimal? cash = null;
        if (CashInput.Text.Trim().Length > 0)
        {
            cash = Money.Parse(due.Currency, CashInput.Text);
            if (cash is null || cash < due.Amount)
            {
                UpdateChange();
                CashInput.Focus();
                return;
            }
        }

        var outcome = await Context.Ui.RunBusyAsync(() => model.ChargeAsync(cash), CafeteriaText.Charging).ConfigureAwait(true);
        if (outcome.Receipt is { } receipt)
        {
            paid = receipt;
            printError = null;
            printStatus = Context.IsSample ? CafeteriaText.SampleNotPrinted : CafeteriaText.Printing;
            CashInput.Text = "";
            UpdateOrder();
            ShowPaid(receipt);
            await Dispatcher.Yield(DispatcherPriority.Background);
            Print(receipt);
            return;
        }

        if (outcome.Shortages.Count > 0)
        {
            await ShowShortagesAsync(outcome.Shortages).ConfigureAwait(true);
        }
        else if (outcome.Error is TaskCanceledException)
        {
            await Context.Ui.ShowMessageAsync(CafeteriaText.ChargeTimeoutTitle, CafeteriaText.ChargeTimeoutText).ConfigureAwait(true);
        }
        else if (outcome.Error is { } error)
        {
            Context.Ui.ShowError(error);
        }
    }

    private void Print(ReceiptDto receipt)
    {
        if (Context.IsSample)
        {
            return;
        }

        try
        {
            printer.Print(ReceiptLayout.ForSale(receipt, Context.Language), Context.Station.PrinterName);
            printStatus = CafeteriaText.ReceiptPrinted;
            printError = null;
        }
        catch (ReceiptPrintException ex)
        {
            printError = ex.Text.For(Context.Language) + (ex.Detail is { Length: > 0 } detail ? " " + detail : "");
            Context.Ui.Toast(CafeteriaText.PrintFailedDetail.Format(Context.Language, printError), ToastKind.Error);
        }

        if (paid is not null)
        {
            ShowPaid(paid);
        }
    }

    private async Task ShowShortagesAsync(IReadOnlyList<Shortage> shortages)
    {
        var language = Context.Language;
        var list = new StackPanel();
        foreach (var shortage in shortages)
        {
            var row = new DockPanel { Margin = new Thickness(0, 0, 0, 10) };
            var detail = new TextBlock
            {
                Text = BidiText.FixNumbers(CafeteriaText.ShortageDetail.Format(language, NumberText.Format(shortage.InCart), NumberText.Format(shortage.Available)), language),
                FontSize = 14,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(12, 0, 0, 0),
            };
            detail.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Bad");
            DockPanel.SetDock(detail, Dock.Right);
            var name = new TextBlock { FontSize = 15, FontWeight = FontWeights.SemiBold, HorizontalAlignment = HorizontalAlignment.Left, TextTrimming = TextTrimming.CharacterEllipsis };
            name.SetResourceReference(TextBlock.ForegroundProperty, "Brush.Text");
            BidiText.SetText(name, ReceiptLayout.ProductName(shortage.NameEn, shortage.NameAr, language));
            row.Children.Add(detail);
            row.Children.Add(name);
            list.Children.Add(row);
        }

        await Context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = CafeteriaText.NotEnoughStockTitle,
            Message = CafeteriaText.NotEnoughStockText,
            Content = list,
            PrimaryText = CommonText.Ok,
            SecondaryText = null,
        }).ConfigureAwait(true);
    }

    private async Task ReloadProductAsync(int productId)
    {
        if (!loaded)
        {
            return;
        }

        try
        {
            model.ApplyProduct(await Context.Api.GetProductAsync(productId).ConfigureAwait(true));
        }
#pragma warning disable CA1031 // A live refresh that fails is caught up by the next one or the next page visit.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    private async Task RefreshShiftQuietlyAsync()
    {
        if (!loaded)
        {
            return;
        }

        try
        {
            await model.RefreshShiftAsync().ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Same as above.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    // Barcode scanner: a keyboard wedge on the whole window, active while this screen is visible.

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Context.IsSample || window is not null)
        {
            return;
        }

        window = Window.GetWindow(this);
        if (window is not null)
        {
            window.PreviewTextInput += OnWindowTextInput;
            window.PreviewKeyDown += OnWindowKeyDown;
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (window is not null)
        {
            window.PreviewTextInput -= OnWindowTextInput;
            window.PreviewKeyDown -= OnWindowKeyDown;
            window = null;
        }

        wedge.Reset();
    }

    /// <summary>The scanner counts while this screen shows and the typing is not going into a dialog's field.</summary>
    private bool ScannerActive()
    {
        if (!IsVisible || !loaded)
        {
            return false;
        }

        return Keyboard.FocusedElement is not (TextBoxBase or PasswordBox) || IsAncestorOf((DependencyObject)Keyboard.FocusedElement);
    }

    private void OnWindowTextInput(object sender, TextCompositionEventArgs e)
    {
        if (!ScannerActive())
        {
            return;
        }

        if (wedge.Feed(e.Text, e.Timestamp))
        {
            beforeScan = Keyboard.FocusedElement is TextBox box ? (box, box.Text, box.CaretIndex) : null;
        }
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || !ScannerActive())
        {
            return;
        }

        var code = wedge.Complete(e.Timestamp);
        if (code is null)
        {
            return;
        }

        // A scan: take back what the scanner typed into a text box, then add the item.
        e.Handled = true;
        if (beforeScan is { } before)
        {
            before.Box.Text = before.Text;
            before.Box.CaretIndex = Math.Min(before.Caret, before.Text.Length);
        }

        beforeScan = null;
        _ = HandleScanAsync(code);
    }

    // Controls

    private void OnTilesSizeChanged(object sender, SizeChangedEventArgs e) =>
        TileColumns = Math.Clamp((int)(e.NewSize.Width / MinTileWidth), 2, 8);

    private void OnTileClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ProductTile tile })
        {
            AddProduct(tile.Product);
        }
    }

    private void OnCategoryChecked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CategoryTab tab })
        {
            model.CategoryId = tab.Id;
        }
    }

    private void OnSearchChanged(object? sender, EventArgs e) => model.Search = SearchInput.Text;

    /// <summary>Enter in the search box with a whole barcode (typed by hand) adds the item.</summary>
    private void OnSearchKeyDown(object sender, KeyEventArgs e)
    {
        var text = SearchInput.Text.Trim();
        if (e.Key != Key.Enter || text.Length == 0)
        {
            return;
        }

        if (model.Products.FirstOrDefault(p => string.Equals(p.Barcode, text, StringComparison.Ordinal)) is { } product)
        {
            e.Handled = true;
            SearchInput.Text = "";
            model.Search = "";
            AddProduct(product);
        }
    }

    private void OnDecreaseClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CartLine line })
        {
            model.Cart.Decrease(line);
        }
    }

    private void OnIncreaseClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: CartLine line } && !model.Cart.Increase(line))
        {
            var name = line.Name;
            Warn(CafeteriaText.NoMoreInStock.Format(Context.Language, name, NumberText.Format(line.Product.Stock)));
        }
    }

    private async void OnClearClick(object sender, RoutedEventArgs e)
    {
        if (model.Cart.IsEmpty)
        {
            return;
        }

        if (await Context.Ui.ConfirmAsync(CafeteriaText.ClearOrderTitle, CafeteriaText.ClearOrderText, CommonText.Clear, danger: true).ConfigureAwait(true))
        {
            model.Cart.Clear();
            CashInput.Text = "";
        }
    }

    private void OnQuickCashClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: QuickCash quick })
        {
            CashInput.Text = quick.Text;
            CashInput.CaretIndex = CashInput.Text.Length;
        }
    }

    /// <summary>Dollars or pounds: the cash typed so far was in the other currency, so it is cleared.</summary>
    private void OnCurrencyChanged(object? sender, EventArgs e)
    {
        if (model.PayCurrency == CurrencyInput.Currency)
        {
            return;
        }

        model.PayCurrency = CurrencyInput.Currency;
        CashInput.Text = "";
        UpdateOrder();
    }

    private void OnCashChanged(object sender, TextChangedEventArgs e) => UpdateChange();

    private async void OnChargeClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await ChargeAsync().ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Show any failure; the cart stays as it was.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Context.Ui.ShowError(ex);
        }
    }

    private void OnReprintClick(object sender, RoutedEventArgs e)
    {
        if (model.LastReceipt is { } receipt)
        {
            printStatus = CafeteriaText.Printing;
            printError = null;
            Print(receipt);
        }
    }

    private void OnNewOrderClick(object sender, RoutedEventArgs e)
    {
        StartNewOrder();
        if (!Context.IsSample)
        {
            SearchInput.Focus();
        }
    }
}
