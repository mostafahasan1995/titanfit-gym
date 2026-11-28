using System.Windows;
using System.Windows.Input;
using Gym.Admin.Logic;
using Gym.Client.Sample;
using Gym.Contracts;
using Gym.Domain;
using Gym.UI;
using Gym.UI.Controls;
using Gym.UI.Shell;

namespace Gym.Admin.Pages;

public static class ExchangeRateText
{
    public static LocalText TodayTitle { get; } = new("Today's exchange rate", "سعر صرف اليوم");

    public static LocalText Intro { get; } = new(
        "Enter the rate by hand every day. All prices are in US dollars; every PC shows the Syrian pounds at the newest rate.",
        "أدخل السعر يدويًا كل يوم. كل الأسعار بالدولار، وتعرض كل الأجهزة ما يعادلها بالليرة السورية بأحدث سعر.");

    public static LocalText PerDollar { get; } = new("for $1", "لكل 1 دولار");

    /// <summary>{0} = the time, {1} = who entered it.</summary>
    public static LocalText StateToday { get; } = new("Entered today at {0} by {1}.", "أُدخل اليوم الساعة {0} بواسطة {1}.");

    /// <summary>{0} = the day of the last rate.</summary>
    public static LocalText StateStale { get; } = new(
        "Not entered today: the last rate, from {0}, is used. Enter today's rate below.",
        "لم يُدخَل سعر اليوم: يُستخدم آخر سعر، من {0}. أدخل سعر اليوم في الأسفل.");

    public static LocalText StateNone { get; } = new(
        "No rate yet. Amounts in Syrian pounds are hidden on every PC and only US dollars can be taken until a rate is entered.",
        "لا يوجد سعر بعد. المبالغ بالليرة السورية مخفية في كل الأجهزة ويُقبض بالدولار فقط حتى يُدخَل سعر.");

    public static LocalText PillToday { get; } = new("Today", "اليوم");

    public static LocalText PillStale { get; } = new("Old rate", "سعر قديم");

    public static LocalText PillNone { get; } = new("No rate", "لا يوجد سعر");

    public static LocalText NewRate { get; } = new("Syrian pounds for $1", "عدد الليرات السورية لكل 1 دولار");

    public static LocalText NewRateHint { get; } = new(
        "Type today's rate, e.g. 11500. Saving adds it to the history; the newest rate is used at once.",
        "اكتب سعر اليوم، مثل 11500. يُضاف عند الحفظ إلى السجل ويُستخدم أحدث سعر فورًا.");

    public static LocalText SaveRate { get; } = new("Save today's rate", "حفظ سعر اليوم");

    /// <summary>{0} = the pounds for 1 dollar.</summary>
    public static LocalText RateSaved { get; } = new("Today's rate saved: $1 = {0}", "تم حفظ سعر اليوم: 1 دولار = {0}");

    /// <summary>{0} = a dollar amount, {1} = the pounds, {2} = the rounding step in pounds.</summary>
    public static LocalText Example { get; } = new(
        "Example: {0} = {1}. Amounts in pounds are rounded to the nearest {2} (Settings page).",
        "مثال: {0} = {1}. تُقرَّب المبالغ بالليرة إلى أقرب {2} (صفحة الإعدادات).");

    public static LocalText ExampleNone { get; } = new(
        "Type a rate to see what a $20.00 payment is in Syrian pounds.",
        "اكتب سعرًا لترى ما يعادل دفعة بقيمة 20.00 دولار بالليرة السورية.");

    public static LocalText HistoryTitle { get; } = new("History", "السجل");

    /// <summary>{0} = the number of entries, {1} = the days.</summary>
    public static LocalText HistoryCount { get; } = new("{0} entries in the last {1} days", "عدد الإدخالات في آخر {1} يومًا: {0}");

    public static LocalText ColumnDate { get; } = new("Date", "التاريخ");

    public static LocalText ColumnRate { get; } = new("Rate for $1", "السعر لكل 1 دولار");

    public static LocalText ColumnBy { get; } = new("Entered by", "أدخله");

    public static LocalText ColumnTime { get; } = new("Time", "الوقت");

    public static LocalText NoHistory { get; } = new("No rates yet", "لا توجد أسعار بعد");

    public static LocalText NoHistoryText { get; } = new("Today's rate appears here once it is saved.", "يظهر سعر اليوم هنا بعد حفظه.");
}

/// <summary>The daily exchange rate: today's state, entering today's rate, and the history (last 90 days).</summary>
public partial class ExchangeRatePage : PageView
{
    /// <summary>How far back the history goes.</summary>
    public const int HistoryDays = 90;

    /// <summary>The dollar amount of the worked example.</summary>
    public const decimal ExampleUsd = 20m;

    private ExchangeRateStatusDto? status;
    private IReadOnlyList<ExchangeRateDto> history = [];

    public ExchangeRatePage(PageContext context)
        : base(context)
    {
        InitializeComponent();
        RateBox.TextChanged += (_, _) => ShowExample();
    }

    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        status = await Context.Api.GetExchangeRateAsync(cancellationToken).ConfigureAwait(true);
        ExchangeRates.Current.Set(status); // the shell's chip and banner follow at once
        var today = status.Today;
        history = await Context.Api.GetExchangeRatesAsync(today.AddDays(-HistoryDays), today, cancellationToken).ConfigureAwait(true);
        Show();
    }

    public override void OnLanguageChanged()
    {
        Show();
        if (RateField.Error is not null)
        {
            RateField.Error = FormChecks.Rate(RateBox.Text).Error?.For(Context.Language);
        }
    }

    /// <summary>Smoke screenshots: the page (and the shell banner) with an older rate or with no rate at all.</summary>
    internal async Task ShowStateForSmokeAsync(ExchangeRateState state)
    {
        if (Context.Api is SampleGymApi sample)
        {
            sample.SimulateExchangeRate(state);
        }

        await OnShownAsync(CancellationToken.None).ConfigureAwait(true);
        await Dispatcher.InvokeAsync(() => { }, System.Windows.Threading.DispatcherPriority.Background);
    }

    private void Show()
    {
        var language = Context.Language;
        var rate = status?.Rate;
        var state = status?.State ?? ExchangeRateState.None;
        if (rate is not null)
        {
            RateValue.Text = Money.FormatSyp(rate.SypPerUsd, language);
            RatePanel.Visibility = Visibility.Visible;
        }
        else
        {
            RatePanel.Visibility = Visibility.Collapsed;
        }

        (StatePill.Tone, StatePill.Text) = state switch
        {
            ExchangeRateState.Today => (PillTone.Good, ExchangeRateText.PillToday.For(language)),
            ExchangeRateState.Stale => (PillTone.Warning, ExchangeRateText.PillStale.For(language)),
            _ => (PillTone.Bad, ExchangeRateText.PillNone.For(language)),
        };
        StateText.Text = BidiText.FixNumbers(
            state switch
            {
                ExchangeRateState.Today when rate is not null => ExchangeRateText.StateToday.Format(language, DateText.Time(rate.EnteredAt), rate.EnteredBy),
                ExchangeRateState.Stale when rate is not null => ExchangeRateText.StateStale.Format(language, DateText.ShortDate(rate.Date, language)),
                _ => ExchangeRateText.StateNone.For(language),
            },
            language);
        StateText.SetResourceReference(
            System.Windows.Controls.TextBlock.ForegroundProperty,
            state == ExchangeRateState.Today ? "Brush.TextTertiary" : "Brush.Warning");

        HistoryGrid.ItemsSource = history.Select(r => new HistoryRow(
            DateText.ShortDate(r.Date, language),
            Money.FormatSyp(r.SypPerUsd, language),
            r.EnteredBy,
            BidiText.HasRtl(r.EnteredBy) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            DateText.Time(r.EnteredAt))).ToList();
        HistoryEmpty.Visibility = history.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        HistoryGrid.Visibility = history.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        HistoryCount.Text = BidiText.FixNumbers(
            ExchangeRateText.HistoryCount.Format(language, NumberText.Format(history.Count), NumberText.Format(HistoryDays)), language);
        ShowExample();
    }

    /// <summary>$20.00 in pounds at the typed rate (or the one in use), with the rounding step from the settings.</summary>
    private void ShowExample()
    {
        var language = Context.Language;
        var typed = FormChecks.Rate(RateBox.Text).Value;
        var rate = typed ?? status?.Rate?.SypPerUsd;
        var step = status?.SypRoundingStep ?? CurrencyRules.DefaultSypRoundingStep;
        ExampleText.Text = rate is { } r
            ? BidiText.FixNumbers(
                ExchangeRateText.Example.Format(
                    language,
                    Money.Format(ExampleUsd, language),
                    Money.FormatSyp(CurrencyRules.ToSyp(ExampleUsd, r, step), language),
                    Money.FormatSyp(step, language)),
                language)
            : BidiText.FixNumbers(ExchangeRateText.ExampleNone.For(language), language);
    }

    private async Task SaveAsync()
    {
        var language = Context.Language;
        var (rate, error) = FormChecks.Rate(RateBox.Text);
        RateField.Error = error?.For(language);
        if (rate is not { } value)
        {
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            var saved = await Context.Api.SetExchangeRateAsync(new SetExchangeRateRequest(value)).ConfigureAwait(true);
            RateBox.Text = "";
            RateField.Error = null;
            Context.Ui.Toast(BidiText.FixNumbers(ExchangeRateText.RateSaved.Format(language, Money.FormatSyp(saved.SypPerUsd, language)), language));
            await OnShownAsync(CancellationToken.None).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Show any failure as a toast; the form keeps what was typed.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Context.Ui.Toast(AdminErrors.Describe(ex, language), ToastKind.Error);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e) => await SaveAsync().ConfigureAwait(true);

    private async void OnRateKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SaveAsync().ConfigureAwait(true);
        }
    }

    /// <summary>A row of the history.</summary>
    public sealed record HistoryRow(string Date, string Rate, string By, FlowDirection ByDirection, string Time);
}
