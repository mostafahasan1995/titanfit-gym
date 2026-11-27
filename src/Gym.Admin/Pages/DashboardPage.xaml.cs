using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Gym.Admin.Logic;
using Gym.Client;
using Gym.Contracts;
using Gym.Domain;
using Gym.UI;
using Gym.UI.Controls;
using Gym.UI.Shell;
using LiveChartsCore;
using LiveChartsCore.Measure;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.WPF;
using SkiaSharp;

namespace Gym.Admin.Pages;

public static class DashboardText
{
    public static LocalText CheckInsToday { get; } = new("Check-ins today", "الدخول اليوم");

    public static LocalText BusiestHour { get; } = new("Busiest hour {0}", "ساعة الذروة {0}");

    public static LocalText NoCheckInsYet { get; } = new("No check-ins yet", "لا يوجد دخول بعد");

    public static LocalText ActiveMembers { get; } = new("Active members", "المشتركون الفعّالون");

    public static LocalText OfWithSubscription { get; } = new("of {0} with a subscription", "من أصل {0} لديهم اشتراك");

    public static LocalText ExpiringSoon { get; } = new("Expiring soon", "تنتهي قريبًا");

    public static LocalText WithinDays { get; } = new("Within {0} · renew soon", "خلال {0} · بحاجة إلى تجديد");

    public static LocalText Expired { get; } = new("Expired", "منتهية");

    public static LocalText NotRenewed { get; } = new("Not renewed", "لم تُجدَّد");

    public static LocalText RevenueToday { get; } = new("Revenue today", "إيراد اليوم");

    public static LocalText RevenueSplit { get; } = new("Subscriptions {0} · Cafeteria {1}", "الاشتراكات {0} · الكافتيريا {1}");

    public static LocalText ChartTitle { get; } = new("Check-ins by hour · today", "الدخول حسب الساعة · اليوم");

    public static LocalText Peak { get; } = new("Busiest {0} · {1} check-ins", "الذروة {0} · عدد الدخول {1}");

    public static LocalText MonthTitle { get; } = new("Revenue this month", "إيراد هذا الشهر");

    public static LocalText MonthRange { get; } = new("{0} to {1}", "من {0} إلى {1}");

    public static LocalText Memberships { get; } = new("Memberships", "الاشتراكات");

    public static LocalText PrivateTraining { get; } = new("Private training", "التدريب الخاص");

    public static LocalText Cafeteria { get; } = new("Cafeteria", "الكافتيريا");

    public static LocalText ExpiringTitle { get; } = new("Expiring in the next {0}", "تنتهي خلال {0}");

    public static LocalText Member { get; } = new("Member", "المشترك");

    public static LocalText Phone { get; } = new("Phone", "الهاتف");

    public static LocalText Ends { get; } = new("Ends", "ينتهي في");

    public static LocalText TimeLeft { get; } = new("Time left", "المتبقي");

    public static LocalText NoneExpiring { get; } = new("Nobody expires soon", "لا أحد ينتهي اشتراكه قريبًا");

    public static LocalText NoneExpiringText { get; } = new("Members whose subscription ends soon show up here.", "يظهر هنا المشتركون الذين يقترب انتهاء اشتراكهم.");

    public static LocalText LowStock { get; } = new("Low stock", "مخزون منخفض");

    public static LocalText LowItems { get; } = new("{0} items", "عدد الأصناف {0}");

    public static LocalText LeftOfMin { get; } = new("{0} left · min {1}", "المتبقي {0} · الحد الأدنى {1}");

    public static LocalText StockOk { get; } = new("Stock is fine", "المخزون جيد");

    public static LocalText StockOkText { get; } = new("No item is below its minimum.", "لا يوجد صنف تحت الحد الأدنى.");

    public static LocalText BackupFailedTitle { get; } = new("The last backup failed", "فشل آخر نسخ احتياطي");

    public static LocalText BackupTooOldTitle { get; } = new("No backup for more than 2 days", "لا يوجد نسخ احتياطي منذ أكثر من يومين");

    public static LocalText BackupNeverTitle { get; } = new("No backup yet", "لا يوجد نسخ احتياطي بعد");

    public static LocalText BackupLastGood { get; } = new("Last good backup: {0}. Open Backups to check the drive.", "آخر نسخة ناجحة: {0}. افتح صفحة النسخ الاحتياطي وتحقّق من القرص.");

    public static LocalText BackupNeverText { get; } = new("Open Backups and press Back up now.", "افتح صفحة النسخ الاحتياطي واضغط «نسخ احتياطي الآن».");
}

/// <summary>The admin dashboard. Refreshes on live events and once a minute while it is shown.</summary>
public partial class DashboardPage : PageView
{
    private const int FirstHour = 6;
    private const int LastHour = 23;

    private readonly CartesianChart chart;
    private readonly DispatcherTimer refreshSoon;
    private readonly DispatcherTimer everyMinute;
    private DashboardDto? dashboard;
    private SettingsDto? settings;
    private BackupStatusDto? backup;
    private (long Memberships, long Private, long Cafeteria, DateOnly From, DateOnly To)? month;
    private bool loading;

    public DashboardPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
        chart = new CartesianChart
        {
            AnimationsSpeed = TimeSpan.Zero,
            EasingFunction = null,
            LegendPosition = LegendPosition.Hidden,
            TooltipPosition = TooltipPosition.Hidden,
            FlowDirection = FlowDirection.LeftToRight,
        };
        ChartHost.Child = chart;

        refreshSoon = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        refreshSoon.Tick += async (_, _) =>
        {
            refreshSoon.Stop();
            await RefreshAsync().ConfigureAwait(true);
        };
        everyMinute = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        everyMinute.Tick += async (_, _) => await RefreshAsync().ConfigureAwait(true);

        Loaded += (_, _) =>
        {
            Subscribe(true);
            everyMinute.Start();
        };
        Unloaded += (_, _) =>
        {
            Subscribe(false);
            everyMinute.Stop();
            refreshSoon.Stop();
        };
    }

    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken).ConfigureAwait(true);
        if (Context.IsSample)
        {
            // The chart draws on its own render tick: give it time before the smoke screenshot.
            await Task.Delay(700, cancellationToken).ConfigureAwait(true);
        }
    }

    public override void OnLanguageChanged() => Show();

    /// <summary>Smoke screenshots: a failed backup (drive unplugged) and the dashboard's warning.</summary>
    internal async Task ShowBackupFailureForSmokeAsync()
    {
        if (Context.Api is Client.Sample.SampleGymApi sample)
        {
            sample.FailNextBackup = true;
            await sample.RunBackupAsync().ConfigureAwait(true);
        }

        await OnShownAsync(CancellationToken.None).ConfigureAwait(true);
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        dashboard = await Context.Api.GetDashboardAsync(ct).ConfigureAwait(true);
        settings = await Context.Api.GetSettingsAsync(ct).ConfigureAwait(true);
        var today = dashboard.Date;
        var first = new DateOnly(today.Year, today.Month, 1);
        var subscriptions = await Context.Api.GetSubscriptionReportAsync(first, today, ct).ConfigureAwait(true);
        var sales = await Context.Api.GetSalesReportAsync(first, today, ct).ConfigureAwait(true);
        month = (
            subscriptions.ByPlan.Where(r => r.Kind == SubscriptionKind.Membership).Sum(r => r.Total),
            subscriptions.ByPlan.Where(r => r.Kind == SubscriptionKind.PrivateTraining).Sum(r => r.Total),
            sales.Total,
            first,
            today);
        try
        {
            backup = await Context.Api.GetBackupStatusAsync(ct).ConfigureAwait(true);
        }
        catch (GymApiException)
        {
            backup = null; // an older server without backups: no banner
        }

        Show();
    }

    private async Task RefreshAsync()
    {
        if (loading || !IsLoaded)
        {
            return;
        }

        loading = true;
        try
        {
            await LoadAsync(CancellationToken.None).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // A failed background refresh keeps the last numbers; the header shows the connection state.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
        finally
        {
            loading = false;
        }
    }

    private void Show()
    {
        if (dashboard is not { } d)
        {
            return;
        }

        var language = Context.Language;
        var threshold = settings?.ExpiringSoonDays ?? 3;

        // Tiles
        CheckInsValue.Text = NumberText.Format(d.CheckInsToday);
        var peak = d.CheckInsByHour.Where(h => h.Count > 0).OrderByDescending(h => h.Count).ThenBy(h => h.Hour).FirstOrDefault();
        CheckInsSub.Text = peak is null ? DashboardText.NoCheckInsYet.For(language) : DashboardText.BusiestHour.Format(language, HourText(peak.Hour));
        ActiveValue.Text = NumberText.Format(d.ActiveMembers);
        ActiveSub.Text = DashboardText.OfWithSubscription.Format(language, NumberText.Format(d.ActiveMembers + d.ExpiringSoonMembers + d.ExpiredMembers));
        ExpiringValue.Text = NumberText.Format(d.ExpiringSoonMembers);
        ExpiringSub.Text = DashboardText.WithinDays.Format(language, AdminText.Days(threshold, language));
        ExpiredValue.Text = NumberText.Format(d.ExpiredMembers);
        RevenueValue.Amount = d.RevenueTotal;
        RevenueSub.Text = DashboardText.RevenueSplit.Format(
            language, Money.FormatAmount(d.RevenueSubscriptions + d.RevenuePrivateTraining), Money.FormatAmount(d.RevenueCafeteria));

        ShowChart(d.CheckInsByHour);
        PeakText.Text = peak is null ? "" : DashboardText.Peak.Format(language, HourText(peak.Hour), NumberText.Format(peak.Count));

        // This month
        if (month is { } m)
        {
            MonthRange.Text = DashboardText.MonthRange.Format(language, DateText.ShortDate(m.From, language), DateText.ShortDate(m.To, language));
            var total = m.Memberships + m.Private + m.Cafeteria;
            MonthTotal.Amount = total;
            MonthRows.ItemsSource = new[]
            {
                ShareRow.Create(DashboardText.Memberships.For(language), m.Memberships, total, ThemeBrush("Brush.Accent")),
                ShareRow.Create(DashboardText.PrivateTraining.For(language), m.Private, total, ThemeBrush("Brush.Turquoise")),
                ShareRow.Create(DashboardText.Cafeteria.For(language), m.Cafeteria, total, ThemeBrush("Brush.Gold")),
            };
        }

        // Expiring soon
        ExpiringTitle.Text = DashboardText.ExpiringTitle.Format(language, AdminText.Days(threshold, language));
        ExpiringCount.Text = AdminText.MemberCount(d.ExpiringSoonMembers, language);
        var expiring = d.ExpiringSoon.Select(s => new ExpiringRow(
            s.FullName,
            BidiText.HasRtl(s.FullName) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight,
            s.MemberNoText,
            PhoneText.Format(s.Phone),
            s.Membership is { } ms ? DateText.ShortDate(ms.EndDate, language) : "",
            s.Membership is { } left ? AdminText.Days(left.DaysLeft, language) : "")).ToList();
        ExpiringGrid.ItemsSource = expiring;
        ExpiringGrid.Visibility = expiring.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        ExpiringEmpty.Visibility = expiring.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        // Low stock
        var low = d.LowStock.OrderBy(l => l.MinStock == 0 ? 1 : (double)l.Stock / l.MinStock).ThenBy(l => l.NameEn, StringComparer.OrdinalIgnoreCase)
            .Select(l => new LowRow(
                language == AppLanguage.Arabic ? l.NameAr : l.NameEn,
                DashboardText.LeftOfMin.Format(language, NumberText.Format(l.Stock), NumberText.Format(l.MinStock)),
                ShareRow.Part(l.Stock, Math.Max(1, l.MinStock)),
                ShareRow.Part(Math.Max(0, l.MinStock - l.Stock), Math.Max(1, l.MinStock)),
                l.Stock == 0 ? ThemeBrush("Brush.Bad") : ThemeBrush("Brush.Warning"),
                l.Stock == 0 ? ThemeBrush("Brush.BadSoft") : ThemeBrush("Brush.WarningSoft")))
            .ToList();
        LowList.ItemsSource = low;
        LowCount.Text = low.Count == 0 ? "" : DashboardText.LowItems.Format(language, NumberText.Format(low.Count));
        LowEmpty.Visibility = low.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        ShowBackupBanner(language);
    }

    private void ShowBackupBanner(AppLanguage language)
    {
        if (backup is not { Warning: not BackupWarning.None } status)
        {
            BackupBanner.Visibility = Visibility.Collapsed;
            return;
        }

        var bad = status.Warning == BackupWarning.LastFailed;
        BackupBanner.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, bad ? "Brush.BadSoft" : "Brush.WarningSoft");
        BackupBanner.SetResourceReference(System.Windows.Controls.Border.BorderBrushProperty, bad ? "Brush.BadLine" : "Brush.WarningLine");
        BackupBannerIcon.SetResourceReference(Icon.ForegroundProperty, bad ? "Brush.Bad" : "Brush.Warning");
        BackupBannerTitle.Text = (status.Warning switch
        {
            BackupWarning.LastFailed => DashboardText.BackupFailedTitle,
            BackupWarning.TooOld => DashboardText.BackupTooOldTitle,
            _ => DashboardText.BackupNeverTitle,
        }).For(language);
        var reason = status.Warning == BackupWarning.LastFailed && AdminErrors.ForCode(status.LastRun?.ErrorCode) is { } why ? why.For(language) + " " : "";
        BackupBannerText.Text = status.LastSuccess is { } good
            ? reason + DashboardText.BackupLastGood.Format(language, DateText.DateAndTime(good.FinishedAt, language))
            : reason + DashboardText.BackupNeverText.For(language);
        BackupBanner.Visibility = Visibility.Visible;
    }

    private void ShowChart(IReadOnlyList<HourCount> hours)
    {
        var withData = hours.Where(h => h.Count > 0).Select(h => h.Hour).ToList();
        var from = Math.Min(FirstHour, withData.Count == 0 ? FirstHour : withData.Min());
        var to = Math.Max(LastHour, withData.Count == 0 ? LastHour : withData.Max());
        var counts = Enumerable.Range(from, to - from + 1).Select(h => hours.FirstOrDefault(x => x.Hour == h)?.Count ?? 0).ToArray();
        var labels = Enumerable.Range(from, to - from + 1).Select(h => h.ToString("00", CultureInfo.InvariantCulture)).ToArray();

        chart.Series =
        [
            new ColumnSeries<int>
            {
                Values = counts,
                Fill = new SolidColorPaint(Sk("Color.Accent")),
                MaxBarWidth = 26,
                Padding = 4,
                Rx = 4,
                Ry = 4,
            },
        ];
        chart.XAxes =
        [
            new Axis
            {
                Labels = labels,
                LabelsPaint = new SolidColorPaint(Sk("Color.TextSecondary")),
                TextSize = 12,
                SeparatorsPaint = null,
                MinStep = 1,
                ForceStepToMin = true,
            },
        ];
        chart.YAxes =
        [
            new Axis
            {
                MinLimit = 0,
                MinStep = 1,
                LabelsPaint = new SolidColorPaint(Sk("Color.TextSecondary")),
                TextSize = 12,
                SeparatorsPaint = new SolidColorPaint(Sk("Color.Line"), 1),
                Labeler = value => value.ToString("0", CultureInfo.InvariantCulture),
            },
        ];
    }

    private void Subscribe(bool on)
    {
        var live = Context.Live;
        if (on)
        {
            live.CheckInRecorded += OnCheckIn;
            live.MemberChanged += OnMemberChanged;
            live.SaleCompleted += OnSale;
            live.StockChanged += OnStock;
            live.ProductChanged += OnProduct;
            live.SettingsChanged += OnSettings;
        }
        else
        {
            live.CheckInRecorded -= OnCheckIn;
            live.MemberChanged -= OnMemberChanged;
            live.SaleCompleted -= OnSale;
            live.StockChanged -= OnStock;
            live.ProductChanged -= OnProduct;
            live.SettingsChanged -= OnSettings;
        }
    }

    private void OnCheckIn(CheckInResultDto e) => RefreshSoon();

    private void OnMemberChanged(MemberChangedEvent e) => RefreshSoon();

    private void OnSale(SaleCompletedEvent e) => RefreshSoon();

    private void OnStock(StockChangedEvent e) => RefreshSoon();

    private void OnProduct(ProductChangedEvent e) => RefreshSoon();

    private void OnSettings(SettingsDto e) => RefreshSoon();

    // Live events come on a background thread; several in a row give one refresh.
    private void RefreshSoon() => Dispatcher.InvokeAsync(() =>
    {
        refreshSoon.Stop();
        refreshSoon.Start();
    });

    private static string HourText(int hour) => hour.ToString("00", CultureInfo.InvariantCulture) + ":00";

    private static Brush ThemeBrush(string key) => Application.Current?.TryFindResource(key) as Brush ?? Brushes.Transparent;

    private static SKColor Sk(string key) =>
        Application.Current?.TryFindResource(key) is Color c ? new SKColor(c.R, c.G, c.B, c.A) : SKColors.White;

    /// <summary>A labelled amount with a bar for its share.</summary>
    public sealed record ShareRow(string Label, long Amount, GridLength Share, GridLength Rest, Brush Brush)
    {
        public static ShareRow Create(string label, long amount, long total, Brush brush) =>
            new(label, amount, Part(amount, Math.Max(1, total)), Part(Math.Max(0, total - amount), Math.Max(1, total)), brush);

        public static GridLength Part(long part, long total) => new(Math.Max(0, (double)part / total), GridUnitType.Star);
    }

    public sealed record ExpiringRow(string Name, FlowDirection NameDirection, string Number, string Phone, string Ends, string Left);

    public sealed record LowRow(string Name, string Detail, GridLength Share, GridLength Rest, Brush Brush, Brush SoftBrush);
}
