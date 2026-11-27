using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Media;
using Gym.Admin.Logic;
using Gym.Contracts;
using Gym.UI;
using Gym.UI.Shell;
using Microsoft.Win32;

namespace Gym.Admin.Pages;

public static class ReportsText
{
    public static LocalText Subscriptions { get; } = new("Subscriptions", "الاشتراكات");

    public static LocalText Attendance { get; } = new("Attendance", "الحضور");

    public static LocalText Sales { get; } = new("Sales", "المبيعات");

    public static LocalText SubscriptionsTitle { get; } = new("Subscriptions report", "تقرير الاشتراكات");

    public static LocalText AttendanceTitle { get; } = new("Attendance report", "تقرير الحضور");

    public static LocalText SalesTitle { get; } = new("Cafeteria sales report", "تقرير مبيعات الكافتيريا");

    public static LocalText Today { get; } = new("Today", "اليوم");

    public static LocalText ThisWeek { get; } = new("This week", "هذا الأسبوع");

    public static LocalText ThisMonth { get; } = new("This month", "هذا الشهر");

    public static LocalText LastMonth { get; } = new("Last month", "الشهر الماضي");

    public static LocalText Custom { get; } = new("Custom", "فترة مخصّصة");

    public static LocalText From { get; } = new("From", "من");

    public static LocalText To { get; } = new("To", "إلى");

    public static LocalText ExportCsv { get; } = new("Export CSV", "تصدير CSV");

    public static LocalText Print { get; } = new("Print", "طباعة");

    public static LocalText RangeInvalid { get; } = new("The end date is before the start date.", "تاريخ النهاية قبل تاريخ البداية.");

    public static LocalText RangeText { get; } = new("{0} to {1}", "من {0} إلى {1}");

    public static LocalText NoData { get; } = new("No data in this period", "لا توجد بيانات في هذه الفترة");

    public static LocalText Totals { get; } = new("Totals", "الإجماليات");

    public static LocalText TotalPayments { get; } = new("Payments", "عدد الدفعات");

    public static LocalText TotalRevenue { get; } = new("Total", "الإجمالي");

    public static LocalText TotalMemberships { get; } = new("Memberships", "الاشتراكات العادية");

    public static LocalText TotalPrivateTraining { get; } = new("Private training", "التدريب الخاص");

    public static LocalText TotalScans { get; } = new("Scans", "عمليات المسح");

    public static LocalText TotalAdmitted { get; } = new("Admitted", "دخول مقبول");

    public static LocalText TotalDenied { get; } = new("Refused", "دخول مرفوض");

    public static LocalText TotalUniqueMembers { get; } = new("Different members", "مشتركون مختلفون");

    public static LocalText TotalSales { get; } = new("Sales", "عدد المبيعات");

    public static LocalText TotalAverageSale { get; } = new("Average sale", "متوسط الفاتورة");

    public static LocalText TotalItemsSold { get; } = new("Items sold", "القطع المباعة");

    public static LocalText ByDay { get; } = new("By day", "حسب اليوم");

    public static LocalText ByPlan { get; } = new("By plan", "حسب الخطة");

    public static LocalText ByStaff { get; } = new("By staff member", "حسب الموظف");

    public static LocalText ByHour { get; } = new("By hour", "حسب الساعة");

    public static LocalText ByItem { get; } = new("By item", "حسب الصنف");

    public static LocalText ByCashier { get; } = new("By cashier", "حسب الكاشير");

    public static LocalText ColumnDate { get; } = new("Date", "التاريخ");

    public static LocalText ColumnPayments { get; } = new("Payments", "الدفعات");

    public static LocalText ColumnTotal { get; } = new("Total", "الإجمالي");

    public static LocalText ColumnPlan { get; } = new("Plan", "الخطة");

    public static LocalText ColumnStaff { get; } = new("Staff member", "الموظف");

    public static LocalText ColumnCheckIns { get; } = new("Check-ins", "الدخول");

    public static LocalText ColumnHour { get; } = new("Hour", "الساعة");

    public static LocalText ColumnItem { get; } = new("Item", "الصنف");

    public static LocalText ColumnQuantity { get; } = new("Qty", "الكمية");

    public static LocalText ColumnSales { get; } = new("Sales", "المبيعات");

    public static LocalText ColumnCashier { get; } = new("Cashier", "الكاشير");

    public static LocalText KindMembership { get; } = new("Membership", "اشتراك");

    public static LocalText KindPrivateTraining { get; } = new("Private training", "تدريب خاص");

    /// <summary>"Membership · 3 months".</summary>
    public static LocalText PlanName { get; } = new("{0} · {1}", "{0} · {1}");

    public static LocalText CsvFilter { get; } = new("CSV file for Excel (*.csv)", "ملف CSV لبرنامج Excel (*.csv)");

    public static LocalText Exported { get; } = new("Report saved.", "تم حفظ التقرير.");

    public static LocalText ExportFailed { get; } = new("Could not save the file: {0}", "تعذّر حفظ الملف: {0}");

    public static LocalText Printed { get; } = new("Printed {0}", "طُبع في {0}");
}

/// <summary>Reports: subscriptions, attendance and sales for a period, with CSV export and print.</summary>
public partial class ReportsPage : PageView
{
    private ReportKind kind = ReportKind.Subscriptions;
    private ReportPreset preset = ReportPreset.ThisMonth;
    private bool updating;
    private int version;
    private object? data;
    private ReportDocument? document;

    public ReportsPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
        updating = true;
        SubscriptionsButton.IsChecked = true;
        MonthButton.IsChecked = true;
        updating = false;
    }

    private enum ReportKind
    {
        Subscriptions,
        Attendance,
        Sales,
    }

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public override Task OnShownAsync(CancellationToken cancellationToken)
    {
        if (preset != ReportPreset.Custom)
        {
            SetDates(ReportPeriod.For(preset, Today));
        }

        return LoadAsync(cancellationToken);
    }

    public override void OnLanguageChanged() => Render();

    private (DateOnly From, DateOnly To) SelectedRange() => (
        FromPicker.SelectedDate is { } from ? DateOnly.FromDateTime(from) : Today,
        ToPicker.SelectedDate is { } to ? DateOnly.FromDateTime(to) : Today);

    private void SetDates((DateOnly From, DateOnly To) range)
    {
        var was = updating;
        updating = true;
        FromPicker.SelectedDate = range.From.ToDateTime(TimeOnly.MinValue);
        ToPicker.SelectedDate = range.To.ToDateTime(TimeOnly.MinValue);
        updating = was;
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var (from, to) = SelectedRange();
        var valid = ReportPeriod.IsValid(from, to);
        RangeErrorText.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
        if (!valid)
        {
            return;
        }

        var mine = ++version;
        object result = kind switch
        {
            ReportKind.Attendance => await Context.Api.GetAttendanceReportAsync(from, to, cancellationToken).ConfigureAwait(true),
            ReportKind.Sales => await Context.Api.GetSalesReportAsync(from, to, cancellationToken).ConfigureAwait(true),
            _ => await Context.Api.GetSubscriptionReportAsync(from, to, cancellationToken).ConfigureAwait(true),
        };
        if (mine != version)
        {
            return; // a newer request is on its way
        }

        data = result;
        Render();
    }

    /// <summary>Smoke screenshots: the attendance or the sales report of this week.</summary>
    internal Task ShowForSmokeAsync(bool sales)
    {
        updating = true;
        (sales ? SalesButton : AttendanceButton).IsChecked = true;
        WeekButton.IsChecked = true;
        updating = false;
        kind = sales ? ReportKind.Sales : ReportKind.Attendance;
        preset = ReportPreset.ThisWeek;
        SetDates(ReportPeriod.For(preset, Today));
        return LoadAsync(CancellationToken.None);
    }

    private async Task ReloadAsync()
    {
        try
        {
            await LoadAsync(CancellationToken.None).ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Any failure is shown as a toast; the page stays usable.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Context.Ui.ShowError(ex);
        }
    }

    private void Render()
    {
        var language = Context.Language;
        document = data switch
        {
            SubscriptionReportDto subscriptions => ReportTables.Subscriptions(subscriptions, language),
            AttendanceReportDto attendance => ReportTables.Attendance(attendance, language),
            SalesReportDto sales => ReportTables.Sales(sales, language),
            _ => null,
        };
        if (document is null)
        {
            return;
        }

        ReportTitleText.Text = document.Title;
        RangeText.Text = document.Range;
        TotalsList.ItemsSource = document.Totals;
        var tables = document.Tables;
        ShowTable(tables.Count > 0 ? tables[0] : null, MainTitle, MainGrid, MainEmpty);
        ShowTable(tables.Count > 1 ? tables[1] : null, SideTitle1, SideGrid1, SideEmpty1);
        if (tables.Count > 2)
        {
            SideCard2.Visibility = Visibility.Visible;
            Grid.SetRowSpan(SideCard1, 1);
            ShowTable(tables[2], SideTitle2, SideGrid2, SideEmpty2);
        }
        else
        {
            SideCard2.Visibility = Visibility.Collapsed;
            Grid.SetRowSpan(SideCard1, 3);
        }
    }

    private static void ShowTable(ReportTable? table, TextBlock title, DataGrid grid, TextBlock empty)
    {
        title.Text = table?.Title ?? "";
        grid.ItemsSource = null;
        grid.Columns.Clear();
        if (table is null)
        {
            empty.Visibility = Visibility.Collapsed;
            return;
        }

        for (var i = 0; i < table.Columns.Count; i++)
        {
            var column = table.Columns[i];
            grid.Columns.Add(new DataGridTextColumn
            {
                Header = column.Header,
                Binding = new Binding($"[{i}].Display") { Mode = BindingMode.OneWay },
                Width = i == 0
                    ? new DataGridLength(1, DataGridLengthUnitType.Star)
                    : new DataGridLength(column.Kind == ReportColumnKind.Money ? 140 : 84),
                ElementStyle = CellStyle(column.Kind),
            });
        }

        grid.ItemsSource = table.Rows;
        empty.Visibility = table.Rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private static Style CellStyle(ReportColumnKind kind)
    {
        var style = new Style(typeof(TextBlock));
        style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
        style.Setters.Add(new Setter(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center));
        if (kind == ReportColumnKind.Money)
        {
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension("Brush.Gold")));
            style.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.SemiBold));
        }
        else if (kind == ReportColumnKind.Number)
        {
            style.Setters.Add(new Setter(TextBlock.ForegroundProperty, new DynamicResourceExtension("Brush.TextTertiary")));
        }

        return style;
    }

    private async Task ExportAsync()
    {
        if (Context.IsSample || document is not { } report)
        {
            return; // no file dialogs in the smoke screenshots
        }

        var language = Context.Language;
        var dialog = new SaveFileDialog
        {
            Filter = ReportsText.CsvFilter.For(language) + "|*.csv",
            FileName = CsvExport.FileName(report),
            DefaultExt = ".csv",
            AddExtension = true,
            OverwritePrompt = true,
        };
        var owner = Window.GetWindow(this);
        var chosen = owner is null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        if (chosen != true)
        {
            return;
        }

        try
        {
            await File.WriteAllBytesAsync(dialog.FileName, CsvExport.ToBytes(CsvExport.Build(report, language))).ConfigureAwait(true);
            Context.Ui.Toast(ReportsText.Exported);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Context.Ui.Toast(ReportsText.ExportFailed.Format(language, ex.Message), ToastKind.Error);
        }
    }

    private void PrintReport()
    {
        if (Context.IsSample || document is not { } report)
        {
            return; // no printer in CI
        }

        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        var flow = BuildPrintDocument(report, Context.Language);
        flow.PageWidth = dialog.PrintableAreaWidth;
        flow.PageHeight = dialog.PrintableAreaHeight;
        flow.ColumnWidth = dialog.PrintableAreaWidth;
        flow.PagePadding = new Thickness(48);
        dialog.PrintDocument(((IDocumentPaginatorSource)flow).DocumentPaginator, "TITANFIT " + report.Title);
    }

    /// <summary>The report on paper: title, period, totals and the tables, black on white, in the UI language.</summary>
    private FlowDocument BuildPrintDocument(ReportDocument report, AppLanguage language)
    {
        var flow = new FlowDocument
        {
            FontFamily = TryFindResource("Font.Body") as FontFamily ?? new FontFamily("Segoe UI"),
            FontSize = 12,
            Foreground = Brushes.Black,
            Background = Brushes.White,
            FlowDirection = language.ToFlowDirection(),
            Language = language.ToXmlLanguage(),
        };
        flow.Blocks.Add(new Paragraph(new Run("TITANFIT · " + report.Title)) { FontSize = 20, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 2) });
        flow.Blocks.Add(new Paragraph(new Run(report.Range)) { FontSize = 13, Margin = new Thickness(0, 0, 0, 14) });
        flow.Blocks.Add(PrintTable(
            [ReportsText.Totals.For(language), ""],
            report.Totals.Select(t => (IReadOnlyList<string>)new[] { t.Label, t.Display })));
        foreach (var table in report.Tables)
        {
            flow.Blocks.Add(new Paragraph(new Run(table.Title)) { FontSize = 14, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 16, 0, 4) });
            flow.Blocks.Add(PrintTable(table.Columns.Select(c => c.Header).ToList(), table.Rows.Select(r => (IReadOnlyList<string>)r.Select(c => c.Display).ToList())));
        }

        flow.Blocks.Add(new Paragraph(new Run(ReportsText.Printed.Format(language, DateText.DateAndTime(DateTime.UtcNow, language))))
        {
            FontSize = 10,
            Foreground = Brushes.DimGray,
            Margin = new Thickness(0, 18, 0, 0),
        });
        return flow;
    }

    private static Table PrintTable(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
    {
        var table = new Table { CellSpacing = 0 };
        for (var i = 0; i < headers.Count; i++)
        {
            table.Columns.Add(new TableColumn { Width = new GridLength(i == 0 ? 2 : 1, GridUnitType.Star) });
        }

        var group = new TableRowGroup();
        var header = new TableRow { FontWeight = FontWeights.Bold, Background = new SolidColorBrush(Color.FromRgb(0xEE, 0xF2, 0xF3)) };
        foreach (var text in headers)
        {
            header.Cells.Add(PrintCell(text));
        }

        group.Rows.Add(header);
        foreach (var row in rows)
        {
            var line = new TableRow();
            foreach (var text in row)
            {
                line.Cells.Add(PrintCell(text));
            }

            group.Rows.Add(line);
        }

        table.RowGroups.Add(group);
        return table;
    }

    private static TableCell PrintCell(string text) => new(new Paragraph(new Run(text)) { Margin = new Thickness(0) })
    {
        Padding = new Thickness(6, 3, 6, 3),
        BorderBrush = Brushes.LightGray,
        BorderThickness = new Thickness(0, 0, 0, 0.5),
    };

    private async void OnKindChecked(object sender, RoutedEventArgs e)
    {
        if (updating)
        {
            return;
        }

        kind = ReferenceEquals(sender, AttendanceButton) ? ReportKind.Attendance
            : ReferenceEquals(sender, SalesButton) ? ReportKind.Sales
            : ReportKind.Subscriptions;
        await ReloadAsync().ConfigureAwait(true);
    }

    private async void OnPresetChecked(object sender, RoutedEventArgs e)
    {
        if (updating)
        {
            return;
        }

        preset = sender switch
        {
            _ when ReferenceEquals(sender, TodayButton) => ReportPreset.Today,
            _ when ReferenceEquals(sender, WeekButton) => ReportPreset.ThisWeek,
            _ when ReferenceEquals(sender, LastMonthButton) => ReportPreset.LastMonth,
            _ when ReferenceEquals(sender, CustomButton) => ReportPreset.Custom,
            _ => ReportPreset.ThisMonth,
        };
        if (preset != ReportPreset.Custom)
        {
            SetDates(ReportPeriod.For(preset, Today));
        }

        await ReloadAsync().ConfigureAwait(true);
    }

    private async void OnDateChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (updating)
        {
            return;
        }

        if (preset != ReportPreset.Custom)
        {
            preset = ReportPreset.Custom;
            updating = true;
            CustomButton.IsChecked = true;
            updating = false;
        }

        await ReloadAsync().ConfigureAwait(true);
    }

    private async void OnExportClick(object sender, RoutedEventArgs e) => await ExportAsync().ConfigureAwait(true);

    private void OnPrintClick(object sender, RoutedEventArgs e) => PrintReport();
}
