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
            [new ReportTotal("الإجمالي", "1,400,000 ل.س", "1400000", true)],
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
        Assert.Equal("الإجمالي,1400000", lines[3]);
        Assert.Equal("حسب الصنف", lines[5]);
        Assert.Equal("الصنف,الكمية", lines[6]);
        Assert.Equal("مياه,12", lines[7]);
        Assert.Equal("\"Protein \"\"Gold\"\", 1 kg\",1250", lines[8]);
        Assert.EndsWith("\r\n", csv, StringComparison.Ordinal);
        Assert.Equal("titanfit-sales-20261101-20261127.csv", CsvExport.FileName(document));
    }

    [Fact]
    public void Subscription_report_splits_memberships_and_private_training_with_raw_numbers()
    {
        var from = new DateOnly(2026, 11, 1);
        var report = new SubscriptionReportDto(
            from,
            Friday,
            4,
            1_950_000,
            [
                new SubscriptionReportRow(SubscriptionKind.Membership, 1, 2, 300_000),
                new SubscriptionReportRow(SubscriptionKind.Membership, 12, 1, 1_400_000),
                new SubscriptionReportRow(SubscriptionKind.PrivateTraining, 2, 1, 250_000),
            ],
            [new DayAmount(from, 3, 1_700_000), new DayAmount(from.AddDays(1), 1, 250_000)],
            [new StaffAmount(2, "سارة منصور", 4, 1_950_000)]);

        var en = ReportTables.Subscriptions(report, AppLanguage.English);
        Assert.Equal("subscriptions", en.FileKey);
        Assert.Equal(new[] { "4", "1950000", "1700000", "250000" }, en.Totals.Select(t => t.Raw));
        Assert.Equal(new[] { false, true, true, true }, en.Totals.Select(t => t.IsMoney));
        Assert.Equal("1,950,000 SYP", en.Totals[1].Display);
        Assert.Equal(3, en.Tables.Count);
        Assert.Equal(new[] { "Membership · 1 month", "Membership · 12 months", "Private training · 2 months" }, en.Tables[1].Rows.Select(r => r[0].Display));
        Assert.Equal("2026-11-01", en.Tables[0].Rows[0][0].Raw);
        Assert.Equal("1700000", en.Tables[0].Rows[0][2].Raw);

        var ar = ReportTables.Subscriptions(report, AppLanguage.Arabic);
        Assert.Equal("اشتراك · 12 شهرًا", ar.Tables[1].Rows[1][0].Display);
        Assert.Equal("1950000", ar.Totals[1].Raw);
        Assert.Contains("سارة منصور,4,1950000", CsvExport.Build(ar, AppLanguage.Arabic), StringComparison.Ordinal);
    }

    [Fact]
    public void Attendance_by_hour_lists_only_hours_with_check_ins_and_sales_average_is_whole_pounds()
    {
        var day = new DateOnly(2026, 11, 27);
        var attendance = ReportTables.Attendance(
            new AttendanceReportDto(day, day, 5, 4, 1, 3, [new DayCount(day, 4)], [.. Enumerable.Range(0, 24).Select(h => new HourCount(h, h is 7 or 18 ? 2 : 0))]),
            AppLanguage.English);
        Assert.Equal(new[] { "07:00", "18:00" }, attendance.Tables[1].Rows.Select(r => r[0].Display));
        Assert.Equal(day.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture), attendance.Range);

        var sales = ReportTables.Sales(
            new SalesReportDto(day, day, 3, 100_000, [new DayAmount(day, 3, 100_000)], [new ProductSalesDto(1, "Water", "مياه", 7, 35_000)], []),
            AppLanguage.Arabic);
        Assert.Equal(new[] { "3", "100000", "33333", "7" }, sales.Totals.Select(t => t.Raw));
        Assert.Equal("مياه", sales.Tables[0].Rows[0][0].Display);
    }

    [Fact]
    public void Every_admin_string_has_english_and_arabic_with_the_same_placeholders()
    {
        Assert.Empty(LocalTextAudit.FindProblems(typeof(AdminText).Assembly));
    }
}
