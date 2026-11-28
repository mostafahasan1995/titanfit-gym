using System.Globalization;
using Gym.Admin.Pages;
using Gym.Contracts;
using Gym.Domain;
using Gym.UI;

namespace Gym.Admin.Logic;

/// <summary>How a column's values are written: text, a count, or money (US dollars, or pounds in the currency table).</summary>
public enum ReportColumnKind
{
    Text,
    Number,
    Money,
}

public sealed record ReportColumn(string Header, ReportColumnKind Kind);

/// <summary>A value as shown on screen and on paper (<see cref="Display"/>) and as written to the CSV file (<see cref="Raw"/>).</summary>
public sealed record ReportCell(string Display, string Raw);

public sealed record ReportTable(string Title, IReadOnlyList<ReportColumn> Columns, IReadOnlyList<IReadOnlyList<ReportCell>> Rows);

public sealed record ReportTotal(string Label, string Display, string Raw, bool IsMoney);

/// <summary>One report ready for the screen, the CSV export and the print-out. <see cref="FileKey"/> names the file.</summary>
public sealed record ReportDocument(
    string FileKey,
    string Title,
    string Range,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<ReportTotal> Totals,
    IReadOnlyList<ReportTable> Tables);

/// <summary>Turns the server's report answers into titled totals and tables in the chosen language.</summary>
public static class ReportTables
{
    public static ReportDocument Subscriptions(SubscriptionReportDto report, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(report);
        var memberships = report.ByPlan.Where(r => r.Kind == SubscriptionKind.Membership).Sum(r => r.Total);
        var privateTraining = report.ByPlan.Where(r => r.Kind == SubscriptionKind.PrivateTraining).Sum(r => r.Total);
        var count = ReportsText.ColumnPayments.For(language);
        var total = ReportsText.ColumnTotal.For(language);

        var byDay = new ReportTable(
            ReportsText.ByDay.For(language),
            [new(ReportsText.ColumnDate.For(language), ReportColumnKind.Text), new(count, ReportColumnKind.Number), new(total, ReportColumnKind.Money)],
            report.ByDay.Select(d => Row(DateCell(d.Date, language), NumberCell(d.Count), MoneyCell(d.Total, language))).ToList());
        var byPlan = new ReportTable(
            ReportsText.ByPlan.For(language),
            [new(ReportsText.ColumnPlan.For(language), ReportColumnKind.Text), new(count, ReportColumnKind.Number), new(total, ReportColumnKind.Money)],
            report.ByPlan.Select(p => Row(TextCell(PlanName(p.Kind, p.Months, language)), NumberCell(p.Count), MoneyCell(p.Total, language))).ToList());
        var byStaff = new ReportTable(
            ReportsText.ByStaff.For(language),
            [new(ReportsText.ColumnStaff.For(language), ReportColumnKind.Text), new(count, ReportColumnKind.Number), new(total, ReportColumnKind.Money)],
            report.ByStaff.Select(s => Row(TextCell(s.StaffName), NumberCell(s.Count), MoneyCell(s.Total, language))).ToList());

        return new ReportDocument(
            "subscriptions",
            ReportsText.SubscriptionsTitle.For(language),
            RangeText(report.From, report.To, language),
            report.From,
            report.To,
            [
                NumberTotal(ReportsText.TotalPayments, report.Count, language),
                MoneyTotal(ReportsText.TotalRevenue, report.Total, language),
                MoneyTotal(ReportsText.TotalMemberships, memberships, language),
                MoneyTotal(ReportsText.TotalPrivateTraining, privateTraining, language),
                .. SypTotal(report.Paid, language),
            ],
            [byDay, byPlan, byStaff, .. CurrencyTable(report.Paid, ReportsText.ColumnPayments, language)]);
    }

    public static ReportDocument Attendance(AttendanceReportDto report, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(report);
        var checkIns = ReportsText.ColumnCheckIns.For(language);
        var byDay = new ReportTable(
            ReportsText.ByDay.For(language),
            [new(ReportsText.ColumnDate.For(language), ReportColumnKind.Text), new(checkIns, ReportColumnKind.Number)],
            report.ByDay.Select(d => Row(DateCell(d.Date, language), NumberCell(d.Count))).ToList());
        var byHour = new ReportTable(
            ReportsText.ByHour.For(language),
            [new(ReportsText.ColumnHour.For(language), ReportColumnKind.Text), new(checkIns, ReportColumnKind.Number)],
            report.ByHour.Where(h => h.Count > 0).OrderBy(h => h.Hour)
                .Select(h => Row(TextCell(HourText(h.Hour)), NumberCell(h.Count))).ToList());

        return new ReportDocument(
            "attendance",
            ReportsText.AttendanceTitle.For(language),
            RangeText(report.From, report.To, language),
            report.From,
            report.To,
            [
                NumberTotal(ReportsText.TotalScans, report.TotalScans, language),
                NumberTotal(ReportsText.TotalAdmitted, report.Admitted, language),
                NumberTotal(ReportsText.TotalDenied, report.Denied, language),
                NumberTotal(ReportsText.TotalUniqueMembers, report.UniqueMembers, language),
            ],
            [byDay, byHour]);
    }

    public static ReportDocument Sales(SalesReportDto report, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(report);
        var total = ReportsText.ColumnTotal.For(language);
        var sales = ReportsText.ColumnSales.For(language);
        var byItem = new ReportTable(
            ReportsText.ByItem.For(language),
            [new(ReportsText.ColumnItem.For(language), ReportColumnKind.Text), new(ReportsText.ColumnQuantity.For(language), ReportColumnKind.Number), new(total, ReportColumnKind.Money)],
            report.ByProduct.OrderByDescending(p => p.Total).ThenBy(p => p.NameEn, StringComparer.OrdinalIgnoreCase)
                .Select(p => Row(TextCell(language == AppLanguage.Arabic ? p.NameAr : p.NameEn), NumberCell(p.Quantity), MoneyCell(p.Total, language))).ToList());
        var byDay = new ReportTable(
            ReportsText.ByDay.For(language),
            [new(ReportsText.ColumnDate.For(language), ReportColumnKind.Text), new(sales, ReportColumnKind.Number), new(total, ReportColumnKind.Money)],
            report.ByDay.Select(d => Row(DateCell(d.Date, language), NumberCell(d.Count), MoneyCell(d.Total, language))).ToList());
        var byCashier = new ReportTable(
            ReportsText.ByCashier.For(language),
            [new(ReportsText.ColumnCashier.For(language), ReportColumnKind.Text), new(sales, ReportColumnKind.Number), new(total, ReportColumnKind.Money)],
            report.ByCashier.Select(c => Row(TextCell(c.StaffName), NumberCell(c.Count), MoneyCell(c.Total, language))).ToList());

        return new ReportDocument(
            "sales",
            ReportsText.SalesTitle.For(language),
            RangeText(report.From, report.To, language),
            report.From,
            report.To,
            [
                NumberTotal(ReportsText.TotalSales, report.SaleCount, language),
                MoneyTotal(ReportsText.TotalRevenue, report.Total, language),
                MoneyTotal(ReportsText.TotalAverageSale, report.SaleCount > 0 ? CurrencyRules.RoundUsd(report.Total / report.SaleCount) : 0, language),
                NumberTotal(ReportsText.TotalItemsSold, report.ByProduct.Sum(p => (long)p.Quantity), language),
                .. SypTotal(report.Paid, language),
            ],
            [byItem, byDay, byCashier, .. CurrencyTable(report.Paid, ReportsText.ColumnSales, language)]);
    }

    /// <summary>"Membership · 3 months" / "اشتراك · 3 أشهر".</summary>
    public static string PlanName(SubscriptionKind kind, int months, AppLanguage language) =>
        ReportsText.PlanName.Format(
            language,
            (kind == SubscriptionKind.PrivateTraining ? ReportsText.KindPrivateTraining : ReportsText.KindMembership).For(language),
            AdminText.Months(months, language));

    /// <summary>"1 Nov 2026 to 27 Nov 2026" / "من 1 تشرين الثاني 2026 إلى 27 تشرين الثاني 2026" (one day: just the date).</summary>
    public static string RangeText(DateOnly from, DateOnly to, AppLanguage language) => from == to
        ? DateText.ShortDate(from, language)
        : ReportsText.RangeText.Format(language, DateText.ShortDate(from, language), DateText.ShortDate(to, language));

    /// <summary>"18:00".</summary>
    public static string HourText(int hour) => string.Create(CultureInfo.InvariantCulture, $"{hour:00}:00");

    public static ReportCell TextCell(string text) => new(text, text);

    public static ReportCell NumberCell(long value) => new(NumberText.Format(value), value.ToString(CultureInfo.InvariantCulture));

    /// <summary>US dollars: "$12.50" on screen, "12.50" in the CSV file.</summary>
    public static ReportCell MoneyCell(decimal amount, AppLanguage language) => new(Money.Format(amount, language), UsdRaw(amount));

    /// <summary>Syrian pounds: "230,000 SYP" on screen, "230000" in the CSV file.</summary>
    public static ReportCell SypCell(decimal pounds, AppLanguage language) => new(Money.FormatSyp(pounds, language), SypRaw(pounds));

    /// <summary>
    /// The payments per currency (CSV and print; the screen shows the first three tables): dollars paid in dollars,
    /// pounds paid in pounds with their dollar value. None when the server sent no split.
    /// </summary>
    public static IReadOnlyList<ReportTable> CurrencyTable(CurrencyTotals? paid, LocalText countHeader, AppLanguage language)
    {
        if (paid is null)
        {
            return Array.Empty<ReportTable>();
        }

        return new[]
        {
            new ReportTable(
                ReportsText.ByCurrency.For(language),
                [
                    new(ReportsText.ColumnCurrency.For(language), ReportColumnKind.Text),
                    new(countHeader.For(language), ReportColumnKind.Number),
                    new(ReportsText.ColumnPaid.For(language), ReportColumnKind.Money),
                    new(ReportsText.ColumnUsdValue.For(language), ReportColumnKind.Money),
                ],
                [
                    Row(TextCell(CommonText.CurrencyUsd.For(language)), NumberCell(paid.UsdCount), MoneyCell(paid.Usd, language), MoneyCell(paid.Usd, language)),
                    Row(TextCell(CommonText.CurrencySyp.For(language)), NumberCell(paid.SypCount), SypCell(paid.Syp, language), MoneyCell(paid.SypAsUsd, language)),
                ]),
        };
    }

    public static ReportCell DateCell(DateOnly date, AppLanguage language) =>
        new(DateText.ShortDate(date, language), date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private static IReadOnlyList<ReportCell> Row(params ReportCell[] cells) => cells;

    private static ReportTotal NumberTotal(LocalText label, long value, AppLanguage language) =>
        new(label.For(language), NumberText.Format(value), value.ToString(CultureInfo.InvariantCulture), false);

    private static ReportTotal MoneyTotal(LocalText label, decimal amount, AppLanguage language) =>
        new(label.For(language), Money.Format(amount, language), UsdRaw(amount), true);

    /// <summary>The pounds taken (whole pounds), when the server sent the split.</summary>
    private static List<ReportTotal> SypTotal(CurrencyTotals? paid, AppLanguage language)
    {
        var totals = new List<ReportTotal>();
        if (paid is not null)
        {
            totals.Add(new ReportTotal(ReportsText.TotalPaidInSyp.For(language), Money.FormatSyp(paid.Syp, language), SypRaw(paid.Syp), true));
        }

        return totals;
    }

    private static string UsdRaw(decimal amount) => CurrencyRules.RoundUsd(amount).ToString("0.00", CultureInfo.InvariantCulture);

    private static string SypRaw(decimal pounds) => Math.Round(pounds, 0, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
}
