using System.Windows;
using System.Windows.Controls;
using Gym.Cafeteria;
using Gym.Cafeteria.Receipts;
using Gym.Contracts;
using Gym.Domain;
using Gym.UI;

namespace Gym.Tests.Cafeteria;

public sealed class ReceiptTests
{
    private static readonly DateTime SoldAt = new(2026, 11, 26, 16, 42, 0, DateTimeKind.Utc);

    private const decimal Rate = 11_500m;

    private static readonly ReceiptLineDto[] Lines =
    [
        new ReceiptLineDto(1, "Water 500 ml", "مياه 500 مل", 2, 0.25m, 0.50m),
        new ReceiptLineDto(6, "Chocolate protein shake", "شيك بروتين بالشوكولا", 1, 3.00m, 3.00m),
    ];

    /// <summary>$3.50 paid in dollars (the day's rate was 11,500).</summary>
    private static ReceiptDto Receipt(decimal? cash = 5m) => new(
        1, 1042, SoldAt, "Cafeteria 1", 4, "أحمد قاسم", 7, Lines, 3.50m, cash, cash - 3.50m, Currency.Usd, 3.50m, Rate);

    /// <summary>$3.50 paid in pounds: 40,250 at 11,500, 50,000 handed over.</summary>
    private static ReceiptDto SypReceipt(decimal? cash = 50_000m) => new(
        2, 1043, SoldAt, "Cafeteria 1", 4, "أحمد قاسم", 7, Lines, 3.50m, cash, cash - 40_250m, Currency.Syp, 40_250m, Rate);

    private static ShiftSummaryDto Shift(decimal? counted) => new(
        new ShiftDto(12, 4, "أحمد قاسم", "Cafeteria 1", SoldAt.AddHours(-8), SoldAt, 20m, counted, "Two cups broken"),
        14, 245.50m, 265.50m, counted - 265.50m,
        [new ProductSalesDto(6, "Chocolate protein shake", "شيك بروتين بالشوكولا", 5, 15.00m), new ProductSalesDto(1, "Water 500 ml", "مياه 500 مل", 12, 3.00m)],
        new CurrencyTotals(180.00m, 750_000m, 65.50m, 10, 4));

    [Fact]
    public void The_receipt_has_the_brand_number_station_cashier_items_and_totals()
    {
        var layout = ReceiptLayout.ForSale(Receipt(), AppLanguage.English);
        var rows = layout.Rows;

        Assert.Equal(new ReceiptRow(ReceiptRowKind.Brand, "TITANFIT"), rows[0]);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Info, "Receipt no.", "1042"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Info, "Station", "Cafeteria 1"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Info, "Cashier", "أحمد قاسم"), rows);
        Assert.Contains(rows, r => r is { Kind: ReceiptRowKind.Info, Text: "Date" } && r.Value!.Contains("2026", StringComparison.Ordinal));
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Item, "Water 500 ml"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.ItemDetail, ReceiptLayout.QuantityTimesPrice(2, 0.25m), "$0.50"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Total, "Total", "$3.50"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Total in SYP", "40,250 SYP"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Exchange rate", "$1 = 11,500 SYP"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Payment", "Cash, US dollars"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Cash received", "$5.00"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Change", "$1.50"), rows);
        Assert.Equal(2, rows.Count(r => r.Kind == ReceiptRowKind.Item));
    }

    [Fact]
    public void A_receipt_paid_in_pounds_shows_the_pounds_due_the_rate_and_the_change_in_pounds()
    {
        var rows = ReceiptLayout.ForSale(SypReceipt(), AppLanguage.English).Rows;

        Assert.Contains(new ReceiptRow(ReceiptRowKind.Total, "Total", "$3.50"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Total, "Total in SYP", "40,250 SYP"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Exchange rate", "$1 = 11,500 SYP"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Payment", "Cash, Syrian pounds"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Cash received", "50,000 SYP"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Change", "9,750 SYP"), rows);
        Assert.Equal(40_250m, ReceiptLayout.AmountDue(SypReceipt()));
        Assert.Equal(3.50m, ReceiptLayout.AmountDue(Receipt()));
    }

    [Fact]
    public void The_Arabic_receipt_of_a_sale_paid_in_pounds_reads_in_Arabic()
    {
        var text = ReceiptLayout.ForSale(SypReceipt(), AppLanguage.Arabic).ToPlainText();

        Assert.Contains("المجموع: $3.50", text, StringComparison.Ordinal);
        Assert.Contains("المجموع بالليرة: 40,250 ل.س", text, StringComparison.Ordinal);
        Assert.Contains("سعر الصرف: 1 دولار = 11,500 ل.س", text, StringComparison.Ordinal);
        Assert.Contains("الدفع: نقدًا بالليرة السورية", text, StringComparison.Ordinal);
        Assert.Contains("المبلغ المستلم: 50,000 ل.س", text, StringComparison.Ordinal);
        Assert.Contains("الباقي: 9,750 ل.س", text, StringComparison.Ordinal);
        Assert.DoesNotContain(text, c => c is >= '٠' and <= '٩');
    }

    [Fact]
    public void A_receipt_without_a_rate_has_no_pound_lines()
    {
        var rows = ReceiptLayout.ForSale(Receipt() with { ExchangeRate = null }, AppLanguage.English).Rows;

        Assert.DoesNotContain(rows, r => r.Text is "Total in SYP" or "Exchange rate");
    }

    [Fact]
    public void The_Arabic_receipt_is_in_Arabic_with_Western_digits()
    {
        var layout = ReceiptLayout.ForSale(Receipt(), AppLanguage.Arabic);
        var text = layout.ToPlainText();

        Assert.Equal(AppLanguage.Arabic, layout.Language);
        Assert.Contains("إيصال الكافتيريا", text, StringComparison.Ordinal);
        Assert.Contains("رقم الإيصال: 1042", text, StringComparison.Ordinal);
        Assert.Contains("مياه 500 مل", text, StringComparison.Ordinal);
        Assert.Contains("شيك بروتين بالشوكولا", text, StringComparison.Ordinal);
        Assert.Contains("المجموع: $3.50", text, StringComparison.Ordinal);
        Assert.Contains("الباقي: $1.50", text, StringComparison.Ordinal);
        Assert.Contains("TITANFIT", text, StringComparison.Ordinal);
        Assert.DoesNotContain(text, c => c is >= '٠' and <= '٩');
        Assert.Contains("تشرين الثاني", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_cash_typed_the_receipt_has_no_change_line()
    {
        var rows = ReceiptLayout.ForSale(Receipt(cash: null), AppLanguage.English).Rows;

        Assert.DoesNotContain(rows, r => r.Text == "Change");
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Payment", "Cash, US dollars"), rows);
    }

    [Fact]
    public void Quantity_times_price_stays_left_to_right()
    {
        Assert.Equal("‎2 × 0.25‎", ReceiptLayout.QuantityTimesPrice(2, 0.25m));
        Assert.Equal("‎3 × 1,250.00‎", ReceiptLayout.QuantityTimesPrice(3, 1_250m));
        Assert.Equal("Water", ReceiptLayout.ProductName("Water", "", AppLanguage.Arabic));
        Assert.Equal("مياه", ReceiptLayout.ProductName("Water", "مياه", AppLanguage.Arabic));
    }

    [Fact]
    public void The_shift_report_has_the_totals_the_cash_count_and_the_items_sold()
    {
        var rows = ReceiptLayout.ForShift(Shift(counted: 263.50m), AppLanguage.English, SoldAt).Rows;

        Assert.Contains(new ReceiptRow(ReceiptRowKind.Title, "Shift report (Z)"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Info, "Shift no.", "12"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Sales", "14"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Total, "Sales total", "$245.50"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Opening cash", "$20.00"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Expected cash", "$265.50"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Counted cash", "$263.50"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Difference", "-$2.00"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.ItemDetail, "Qty 12", "$3.00"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Paid in USD", "$180.00"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Paid in SYP", "750,000 SYP"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Paid in SYP (in USD)", "$65.50"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Info, "Note", "Two cups broken"), rows);
    }

    [Fact]
    public void An_uncounted_shift_says_so_and_has_no_difference()
    {
        var rows = ReceiptLayout.ForShift(Shift(counted: null), AppLanguage.Arabic, SoldAt).Rows;

        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "النقد المعدود", "لم يُعَدّ"), rows);
        Assert.DoesNotContain(rows, r => r.Text == "الفرق");
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Item, "شيك بروتين بالشوكولا"), rows);
    }

    [Fact]
    public void The_receipt_view_flows_right_to_left_in_Arabic_and_keeps_Latin_data_left_to_right()
    {
        Sta.Run(() =>
        {
            var view = ReceiptView.Create(ReceiptLayout.ForSale(Receipt(), AppLanguage.Arabic));
            view.Measure(new Size(ReceiptView.PrintWidth, double.PositiveInfinity));
            var texts = ReceiptView.Texts(view);

            Assert.Equal(FlowDirection.RightToLeft, view.FlowDirection);
            Assert.Equal(ReceiptView.PrintWidth, view.Width);
            Assert.True(view.DesiredSize.Height > 250, $"Height {view.DesiredSize.Height}");
            Assert.Contains("مياه 500 مل", texts);
            Assert.Contains(Money.Format(3.50m, AppLanguage.Arabic), texts);
            Assert.Contains("40,250 ل.س", texts);
            Assert.Contains("أحمد قاسم", texts);
            Assert.Contains("شكرًا لزيارتكم", texts);

            var station = FindText(view, "Cafeteria 1");
            Assert.Equal(FlowDirection.LeftToRight, station.FlowDirection);
            Assert.Equal(HorizontalAlignment.Right, station.HorizontalAlignment);
        });
    }

    [Fact]
    public void The_receipt_view_is_80_mm_paper_or_72_mm_print_width()
    {
        Assert.Equal(302.36, ReceiptView.PaperWidth, 2);
        Assert.Equal(272.13, ReceiptView.PrintWidth, 2);
        Sta.Run(() =>
        {
            var view = ReceiptView.Create(ReceiptLayout.TestPage("EPSON TM-T20III", "Cafeteria 2", AppLanguage.English, SoldAt), ReceiptView.PaperWidth);
            var texts = ReceiptView.Texts(view);

            Assert.Equal(FlowDirection.LeftToRight, view.FlowDirection);
            Assert.Contains("EPSON TM-T20III", texts);
            Assert.Contains("لوح بروتين", texts);
            Assert.Contains("Protein bar", texts);
        });
    }

    [Fact]
    public void Printing_in_sample_mode_only_records_the_slip()
    {
        var printer = new PreviewReceiptPrinter();
        printer.Print(ReceiptLayout.ForSale(Receipt(), AppLanguage.English), "Receipt printer");

        Assert.Empty(printer.GetPrinterNames());
        Assert.Single(printer.Printed);
        Assert.Equal("Receipt printer", printer.Printed[0].PrinterName);
    }

    [Fact]
    public void Every_cafeteria_string_has_English_and_Arabic()
    {
        Assert.Empty(LocalTextAudit.FindProblems(typeof(CafeteriaText).Assembly));
        Assert.True(LocalTextAudit.FindTexts(typeof(CafeteriaText).Assembly).Count > 150);
    }

    private static TextBlock FindText(DependencyObject root, string text)
    {
        if (root is TextBlock block && block.Text == text)
        {
            return block;
        }

        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (TryFind(child, text) is { } found)
            {
                return found;
            }
        }

        throw new InvalidOperationException($"No text \"{text}\".");
    }

    private static TextBlock? TryFind(DependencyObject root, string text)
    {
        if (root is TextBlock block && block.Text == text)
        {
            return block;
        }

        foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
        {
            if (TryFind(child, text) is { } found)
            {
                return found;
            }
        }

        return null;
    }
}
