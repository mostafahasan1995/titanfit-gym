using System.Text;
using Gym.Admin;
using Gym.Admin.Logic;
using Gym.Contracts;
using Gym.Domain;
using Gym.UI;

namespace Gym.Tests.Admin;

/// <summary>The reports page: period presets (the week starts on Saturday), the report tables and the CSV export.</summary>
public sealed class ReportLogicTests
{
    // 26 Nov 2026 is a Thursday, so 27 Nov is a Friday and 28 Nov a Saturday.
    private static readonly DateOnly Friday = new(2026, 11, 27);

    [Fact]
    public void Today_is_one_day()
    {
        Assert.Equal((Friday, Friday), ReportPeriod.For(ReportPreset.Today, Friday));
    }

    [Theory]
    [InlineData("2026-11-27", "2026-11-21")] // Friday: back to last Saturday
    [InlineData("2026-11-28", "2026-11-28")] // Saturday: the week starts today
    [InlineData("2026-11-29", "2026-11-28")] // Sunday
    [InlineData("2026-12-03", "2026-11-28")] // Thursday, across the month end
    [InlineData("2027-01-01", "2026-12-26")] // across the year end
    public void This_week_starts_on_saturday(string today, string start)
    {
        var day = DateOnly.Parse(today, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal((DateOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture), day), ReportPeriod.For(ReportPreset.ThisWeek, day));
        Assert.Equal(DayOfWeek.Saturday, ReportPeriod.StartOfWeek(day).DayOfWeek);
    }

    [Fact]
    public void This_month_runs_from_the_first_to_today()
    {
        Assert.Equal((new DateOnly(2026, 11, 1), Friday), ReportPeriod.For(ReportPreset.ThisMonth, Friday));
        Assert.Equal((new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 1)), ReportPeriod.For(ReportPreset.ThisMonth, new DateOnly(2026, 12, 1)));
    }

    [Fact]
    public void Last_month_is_the_whole_previous_month_also_across_the_year_and_in_a_leap_year()
    {
        Assert.Equal((new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31)), ReportPeriod.For(ReportPreset.LastMonth, Friday));
        Assert.Equal((new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 31)), ReportPeriod.For(ReportPreset.LastMonth, new DateOnly(2027, 1, 15)));
        Assert.Equal((new DateOnly(2028, 2, 1), new DateOnly(2028, 2, 29)), ReportPeriod.For(ReportPreset.LastMonth, new DateOnly(2028, 3, 31)));
    }

    [Fact]
    public void Custom_uses_the_picked_dates_and_an_end_before_the_start_is_invalid()
    {
        var from = new DateOnly(2026, 9, 5);
        var to = new DateOnly(2026, 10, 20);
        Assert.Equal((from, to), ReportPeriod.For(ReportPreset.Custom, Friday, from, to));
        Assert.Equal((Friday, Friday), ReportPeriod.For(ReportPreset.Custom, Friday));
        Assert.True(ReportPeriod.IsValid(from, to));
        Assert.True(ReportPeriod.IsValid(to, to));
        Assert.False(ReportPeriod.IsValid(to, from));
    }

    [Theory]
    [InlineData("plain", "plain")]
    [InlineData("", "")]
    [InlineData("Water, 500 ml", "\"Water, 500 ml\"")]
    [InlineData("Say \"hi\"", "\"Say \"\"hi\"\"\"")]
    [InlineData("two\nlines", "\"two\nlines\"")]
    [InlineData(" padded ", "\" padded \"")]
    [InlineData("مياه، 500 مل", "مياه، 500 مل")] // the Arabic comma is not a separator
    public void Csv_fields_are_quoted_only_when_needed(string field, string expected) =>
        Assert.Equal(expected, CsvExport.Escape(field));

    [Fact]
    public void Csv_file_starts_with_the_utf8_bom_and_keeps_arabic()
    {
        var document = new ReportDocument(
            "sales",
            "تقرير المبيعات",
            "من 1 إلى 27",
            new DateOnly(2026, 11, 1),
            Friday,
            [new ReportTotal("الإجمالي", "$1,400.00", "1400.00", true)],
            [
                new ReportTable(
                    "حسب الصنف",
                    [new ReportColumn("الصنف", ReportColumnKind.Text), new ReportColumn("الكمية", ReportColumnKind.Number)],
                    [
                        [new ReportCell("مياه", "مياه"), new ReportCell("12", "12")],
                        [new ReportCell("Protein \"Gold\", 1 kg", "Protein \"Gold\", 1 kg"), new ReportCell("1,250", "1250")],
                    ]),
            ]);

        var csv = CsvExport.Build(document, AppLanguage.Arabic);
        var bytes = CsvExport.ToBytes(csv);

        Assert.Equal(new byte[] { 0xEF, 0xBB, 0xBF }, bytes.Take(3).ToArray());
        Assert.Equal(csv, new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3));
        var lines = csv.Split("\r\n");
        Assert.Equal("تقرير المبيعات,من,2026-11-01,إلى,2026-11-27", lines[0]);
        Assert.Equal("", lines[1]);
        Assert.Equal("الإجماليات", lines[2]);
        Assert.Equal("الإجمالي,1400.00", lines[3]);
        Assert.Equal("حسب الصنف", lines[5]);
        Assert.Equal("الصنف,الكمية", lines[6]);
        Assert.Equal("مياه,12", lines[7]);
        Assert.Equal("\"Protein \"\"Gold\"\", 1 kg\",1250", lines[8]);
        Assert.EndsWith("\r\n", csv, StringComparison.Ordinal);
        Assert.Equal("titanfit-sales-20261101-20261127.csv", CsvExport.FileName(document));
    }

    [Fact]
    public void Subscription_report_splits_memberships_and_private_training_in_dollars_with_raw_numbers()
    {
        var from = new DateOnly(2026, 11, 1);
        var report = new SubscriptionReportDto(
            from,
            Friday,
            4,
            270m,
            [
                new SubscriptionReportRow(SubscriptionKind.Membership, 1, 2, 40m),
                new SubscriptionReportRow(SubscriptionKind.Membership, 12, 1, 180m),
                new SubscriptionReportRow(SubscriptionKind.PrivateTraining, 2, 1, 50m),
            ],
            [new DayAmount(from, 3, 220m), new DayAmount(from.AddDays(1), 1, 50m)],
            [new StaffAmount(2, "سارة منصور", 4, 270m)],
            new CurrencyTotals(90m, 2_070_000m, 180m, 3, 1));

        var en = ReportTables.Subscriptions(report, AppLanguage.English);
        Assert.Equal("subscriptions", en.FileKey);
        Assert.Equal(new[] { "4", "270.00", "220.00", "50.00", "2070000" }, en.Totals.Select(t => t.Raw));
        Assert.Equal(new[] { false, true, true, true, true }, en.Totals.Select(t => t.IsMoney));
        Assert.Equal("$270.00", en.Totals[1].Display);
        Assert.Equal("2,070,000 SYP", en.Totals[4].Display);
        Assert.Equal(4, en.Tables.Count);
        Assert.Equal(new[] { "Membership · 1 month", "Membership · 12 months", "Private training · 2 months" }, en.Tables[1].Rows.Select(r => r[0].Display));
        Assert.Equal("2026-11-01", en.Tables[0].Rows[0][0].Raw);
        Assert.Equal("220.00", en.Tables[0].Rows[0][2].Raw);

        // The currency split: dollars paid in dollars, pounds paid in pounds and their dollar value.
        var byCurrency = en.Tables[3];
        Assert.Equal("By currency paid", byCurrency.Title);
        Assert.Equal(new[] { "US dollars", "3", "$90.00", "$90.00" }, byCurrency.Rows[0].Select(c => c.Display));
        Assert.Equal(new[] { "Syrian pounds", "1", "2,070,000 SYP", "$180.00" }, byCurrency.Rows[1].Select(c => c.Display));
        Assert.Equal(new[] { "Syrian pounds", "1", "2070000", "180.00" }, byCurrency.Rows[1].Select(c => c.Raw));

        var ar = ReportTables.Subscriptions(report, AppLanguage.Arabic);
        Assert.Equal("اشتراك · 12 شهرًا", ar.Tables[1].Rows[1][0].Display);
        Assert.Equal("270.00", ar.Totals[1].Raw);
        Assert.Equal("2,070,000 ل.س", ar.Totals[4].Display);
        var csv = CsvExport.Build(ar, AppLanguage.Arabic);
        Assert.Contains("سارة منصور,4,270.00", csv, StringComparison.Ordinal);
        Assert.Contains("ليرة سورية,1,2070000,180.00", csv, StringComparison.Ordinal);
    }

    [Fact]
    public void A_report_without_the_currency_split_has_no_pound_total()
    {
        var day = new DateOnly(2026, 11, 27);
        var report = ReportTables.Subscriptions(new SubscriptionReportDto(day, day, 0, 0m, [], [], []), AppLanguage.English);
        Assert.Equal(4, report.Totals.Count);
        Assert.Equal(3, report.Tables.Count);
    }

    [Fact]
    public void Attendance_by_hour_lists_only_hours_with_check_ins_and_sales_average_is_rounded_to_cents()
    {
        var day = new DateOnly(2026, 11, 27);
        var attendance = ReportTables.Attendance(
            new AttendanceReportDto(day, day, 5, 4, 1, 3, [new DayCount(day, 4)], [.. Enumerable.Range(0, 24).Select(h => new HourCount(h, h is 7 or 18 ? 2 : 0))]),
            AppLanguage.English);
        Assert.Equal(new[] { "07:00", "18:00" }, attendance.Tables[1].Rows.Select(r => r[0].Display));
        Assert.Equal(day.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture), attendance.Range);

        var sales = ReportTables.Sales(
            new SalesReportDto(day, day, 3, 10m, [new DayAmount(day, 3, 10m)], [new ProductSalesDto(1, "Water", "مياه", 7, 1.75m)], []),
            AppLanguage.Arabic);
        Assert.Equal(new[] { "3", "10.00", "3.33", "7" }, sales.Totals.Select(t => t.Raw));
        Assert.Equal("مياه", sales.Tables[0].Rows[0][0].Display);
    }

    [Fact]
    public void Every_admin_string_has_english_and_arabic_with_the_same_placeholders()
    {
        Assert.Empty(LocalTextAudit.FindProblems(typeof(AdminText).Assembly));
    }
}
