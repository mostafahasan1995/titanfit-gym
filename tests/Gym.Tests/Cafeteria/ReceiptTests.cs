using System.Windows;
using System.Windows.Controls;
using Gym.Cafeteria;
using Gym.Cafeteria.Receipts;
using Gym.Contracts;
using Gym.UI;

namespace Gym.Tests.Cafeteria;

public sealed class ReceiptTests
{
    private static readonly DateTime SoldAt = new(2026, 11, 26, 16, 42, 0, DateTimeKind.Utc);

    private static ReceiptDto Receipt(long? cash = 50_000) => new(
        1, 1042, SoldAt, "Cafeteria 1", 4, "أحمد قاسم", 7,
        [
            new ReceiptLineDto(1, "Water 500 ml", "مياه 500 مل", 2, 3_000, 6_000),
            new ReceiptLineDto(6, "Chocolate protein shake", "شيك بروتين بالشوكولا", 1, 35_000, 35_000),
        ],
        41_000, cash, cash - 41_000);

    private static ShiftSummaryDto Shift(long? counted) => new(
        new ShiftDto(12, 4, "أحمد قاسم", "Cafeteria 1", SoldAt.AddHours(-8), SoldAt, 50_000, counted, "Two cups broken"),
        14, 512_000, 562_000, counted - 562_000,
        [new ProductSalesDto(6, "Chocolate protein shake", "شيك بروتين بالشوكولا", 5, 175_000), new ProductSalesDto(1, "Water 500 ml", "مياه 500 مل", 12, 36_000)]);

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
        Assert.Contains(new ReceiptRow(ReceiptRowKind.ItemDetail, ReceiptLayout.QuantityTimesPrice(2, 3_000), "6,000 SYP"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Total, "Total", "41,000 SYP"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Cash received", "50,000 SYP"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Change", "9,000 SYP"), rows);
        Assert.Equal(2, rows.Count(r => r.Kind == ReceiptRowKind.Item));
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
        Assert.Contains("المجموع: 41,000 ل.س", text, StringComparison.Ordinal);
        Assert.Contains("الباقي: 9,000 ل.س", text, StringComparison.Ordinal);
        Assert.Contains("TITANFIT", text, StringComparison.Ordinal);
        Assert.DoesNotContain(text, c => c is >= '٠' and <= '٩');
        Assert.Contains("تشرين الثاني", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Without_cash_typed_the_receipt_has_no_change_line()
    {
        var rows = ReceiptLayout.ForSale(Receipt(cash: null), AppLanguage.English).Rows;

        Assert.DoesNotContain(rows, r => r.Text == "Change");
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Payment", "Cash"), rows);
    }

    [Fact]
    public void Quantity_times_price_stays_left_to_right()
    {
        Assert.Equal("‎2 × 3,000‎", ReceiptLayout.QuantityTimesPrice(2, 3_000));
        Assert.Equal("Water", ReceiptLayout.ProductName("Water", "", AppLanguage.Arabic));
        Assert.Equal("مياه", ReceiptLayout.ProductName("Water", "مياه", AppLanguage.Arabic));
    }

    [Fact]
    public void The_shift_report_has_the_totals_the_cash_count_and_the_items_sold()
    {
        var rows = ReceiptLayout.ForShift(Shift(counted: 560_000), AppLanguage.English, SoldAt).Rows;

        Assert.Contains(new ReceiptRow(ReceiptRowKind.Title, "Shift report (Z)"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Info, "Shift no.", "12"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Sales", "14"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Total, "Sales total", "512,000 SYP"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Expected cash", "562,000 SYP"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Counted cash", "560,000 SYP"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.Amount, "Difference", "-2,000 SYP"), rows);
        Assert.Contains(new ReceiptRow(ReceiptRowKind.ItemDetail, "Qty 12", "36,000 SYP"), rows);
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
            Assert.Contains("41,000 ل.س", texts);
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
