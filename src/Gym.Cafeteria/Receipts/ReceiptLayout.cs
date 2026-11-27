using System.Globalization;
using System.Text;
using Gym.Contracts;
using Gym.UI;

namespace Gym.Cafeteria.Receipts;

public enum ReceiptRowKind
{
    /// <summary>The TITANFIT name at the top.</summary>
    Brand,

    /// <summary>A centred heading ("Cafeteria receipt", "Items sold").</summary>
    Title,

    /// <summary>Label and value in small type (receipt no., date, station, cashier).</summary>
    Info,

    Separator,

    /// <summary>An item name on its own line.</summary>
    Item,

    /// <summary>"2 × 3,000" under the item name, with the line total at the end.</summary>
    ItemDetail,

    /// <summary>The grand total in big type.</summary>
    Total,

    /// <summary>Label and amount (cash, change, shift figures).</summary>
    Amount,

    /// <summary>Centred small text at the bottom.</summary>
    Footer,
}

/// <summary>One line of a receipt: <see cref="Text"/> at the start side, <see cref="Value"/> (optional) at the end side.</summary>
public sealed record ReceiptRow(ReceiptRowKind Kind, string Text = "", string? Value = null);

/// <summary>
/// What a printed slip says, line by line, in one language: the sale receipt, the shift (Z) report and the printer
/// test page. Pure data: <see cref="ReceiptView"/> draws it (screen preview and Windows printing), and an ESC/POS
/// printer can later print the same rows as text. The brand name stays TITANFIT in both languages.
/// </summary>
public sealed record ReceiptLayout(AppLanguage Language, IReadOnlyList<ReceiptRow> Rows)
{
    public const string BrandName = "TITANFIT";

    public static ReceiptLayout ForSale(ReceiptDto receipt, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var rows = new List<ReceiptRow>
        {
            new(ReceiptRowKind.Brand, BrandName),
            new(ReceiptRowKind.Title, CafeteriaText.ReceiptTitle.For(language)),
            new(ReceiptRowKind.Separator),
            new(ReceiptRowKind.Info, CafeteriaText.ReceiptNumber.For(language), Number(receipt.Number)),
            new(ReceiptRowKind.Info, CafeteriaText.ReceiptDate.For(language), DateText.DateAndTime(receipt.At, language)),
            new(ReceiptRowKind.Info, CafeteriaText.ReceiptStation.For(language), receipt.Station),
            new(ReceiptRowKind.Info, CafeteriaText.ReceiptCashier.For(language), receipt.CashierName),
            new(ReceiptRowKind.Separator),
        };

        foreach (var line in receipt.Lines)
        {
            rows.Add(new(ReceiptRowKind.Item, ProductName(line.NameEn, line.NameAr, language)));
            rows.Add(new(ReceiptRowKind.ItemDetail, QuantityTimesPrice(line.Quantity, line.UnitPrice), Money.Format(line.LineTotal, language)));
        }

        rows.Add(new(ReceiptRowKind.Separator));
        rows.Add(new(ReceiptRowKind.Total, CafeteriaText.Total.For(language), Money.Format(receipt.Total, language)));
        rows.Add(new(ReceiptRowKind.Amount, CafeteriaText.ReceiptPayment.For(language), CafeteriaText.Cash.For(language)));
        if (receipt.CashReceived is { } cash)
        {
            rows.Add(new(ReceiptRowKind.Amount, CafeteriaText.CashReceived.For(language), Money.Format(cash, language)));
            rows.Add(new(ReceiptRowKind.Amount, CafeteriaText.Change.For(language), Money.Format(receipt.Change ?? 0, language)));
        }

        rows.Add(new(ReceiptRowKind.Separator));
        rows.Add(new(ReceiptRowKind.Footer, CafeteriaText.ReceiptThanks.For(language)));
        return new ReceiptLayout(language, rows);
    }

    /// <summary>The shift (Z) report printed when a shift is closed (or again later).</summary>
    public static ReceiptLayout ForShift(ShiftSummaryDto summary, AppLanguage language, DateTime printedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(summary);
        var shift = summary.Shift;
        var rows = new List<ReceiptRow>
        {
            new(ReceiptRowKind.Brand, BrandName),
            new(ReceiptRowKind.Title, CafeteriaText.ZReportTitle.For(language)),
            new(ReceiptRowKind.Separator),
            new(ReceiptRowKind.Info, CafeteriaText.ShiftNumber.For(language), Number(shift.Id)),
            new(ReceiptRowKind.Info, CafeteriaText.ReceiptStation.For(language), shift.Station),
            new(ReceiptRowKind.Info, CafeteriaText.ReceiptCashier.For(language), shift.CashierName),
            new(ReceiptRowKind.Info, CafeteriaText.ShiftOpened.For(language), DateText.DateAndTime(shift.OpenedAt, language)),
            new(ReceiptRowKind.Info, CafeteriaText.ShiftClosed.For(language),
                shift.ClosedAt is { } closed ? DateText.DateAndTime(closed, language) : CafeteriaText.ShiftStillOpen.For(language)),
            new(ReceiptRowKind.Separator),
            new(ReceiptRowKind.Amount, CafeteriaText.SalesCount.For(language), Number(summary.SaleCount)),
            new(ReceiptRowKind.Total, CafeteriaText.SalesTotal.For(language), Money.Format(summary.SalesTotal, language)),
            new(ReceiptRowKind.Amount, CafeteriaText.OpeningCash.For(language), Money.Format(shift.OpeningCash, language)),
            new(ReceiptRowKind.Amount, CafeteriaText.ExpectedCash.For(language), Money.Format(summary.ExpectedCash, language)),
            new(ReceiptRowKind.Amount, CafeteriaText.CountedCash.For(language),
                shift.ClosingCash is { } counted ? Money.Format(counted, language) : CafeteriaText.NotCounted.For(language)),
        };

        if (summary.CashDifference is { } difference)
        {
            rows.Add(new(ReceiptRowKind.Amount, CafeteriaText.CashDifference.For(language), SignedMoney(difference, language)));
        }

        rows.Add(new(ReceiptRowKind.Separator));
        rows.Add(new(ReceiptRowKind.Title, CafeteriaText.ItemsSold.For(language)));
        if (summary.Products.Count == 0)
        {
            rows.Add(new(ReceiptRowKind.Footer, CafeteriaText.NoSalesYet.For(language)));
        }

        foreach (var product in summary.Products)
        {
            rows.Add(new(ReceiptRowKind.Item, ProductName(product.NameEn, product.NameAr, language)));
            rows.Add(new(ReceiptRowKind.ItemDetail, CafeteriaText.PiecesSold.Format(language, Number(product.Quantity)), Money.Format(product.Total, language)));
        }

        if (!string.IsNullOrWhiteSpace(shift.Note))
        {
            rows.Add(new(ReceiptRowKind.Separator));
            rows.Add(new(ReceiptRowKind.Info, CafeteriaText.ShiftNote.For(language), shift.Note.Trim()));
        }

        rows.Add(new(ReceiptRowKind.Separator));
        rows.Add(new(ReceiptRowKind.Footer, CafeteriaText.PrintedAt.Format(language, DateText.DateAndTime(printedAtUtc, language))));
        return new ReceiptLayout(language, rows);
    }

    /// <summary>A short page in both languages to check a printer (Arabic shaping, width, cut).</summary>
    public static ReceiptLayout TestPage(string printerName, string station, AppLanguage language, DateTime nowUtc)
    {
        var rows = new List<ReceiptRow>
        {
            new(ReceiptRowKind.Brand, BrandName),
            new(ReceiptRowKind.Title, CafeteriaText.TestPageTitle.For(language)),
            new(ReceiptRowKind.Separator),
            new(ReceiptRowKind.Info, CafeteriaText.Printer.For(language), printerName),
            new(ReceiptRowKind.Info, CafeteriaText.ReceiptStation.For(language), station),
            new(ReceiptRowKind.Info, CafeteriaText.ReceiptDate.For(language), DateText.DateAndTime(nowUtc, language)),
            new(ReceiptRowKind.Separator),
            new(ReceiptRowKind.Item, CafeteriaText.TestPageSample.En),
            new(ReceiptRowKind.Item, CafeteriaText.TestPageSample.Ar),
            new(ReceiptRowKind.ItemDetail, QuantityTimesPrice(2, 3_000), Money.Format(6_000, language)),
            new(ReceiptRowKind.Separator),
            new(ReceiptRowKind.Footer, CafeteriaText.TestPageFooter.For(language)),
        };
        return new ReceiptLayout(language, rows);
    }

    /// <summary>The item name in the receipt language (the other one when it is empty).</summary>
    public static string ProductName(string nameEn, string nameAr, AppLanguage language)
    {
        var name = language == AppLanguage.Arabic ? nameAr : nameEn;
        var other = language == AppLanguage.Arabic ? nameEn : nameAr;
        return string.IsNullOrWhiteSpace(name) ? other : name;
    }

    /// <summary>"2 × 3,000". Left-to-right marks keep it in that order inside Arabic text.</summary>
    public static string QuantityTimesPrice(int quantity, long unitPrice) =>
        $"‎{Number(quantity)} × {NumberText.Format(unitPrice)}‎";

    /// <summary>The rows as plain text, one per line ("label: value"), for tests and a future ESC/POS printer.</summary>
    public string ToPlainText()
    {
        var text = new StringBuilder();
        foreach (var row in Rows)
        {
            var line = row.Kind == ReceiptRowKind.Separator
                ? new string('-', 32)
                : row.Value is null ? row.Text : $"{row.Text}: {row.Value}";
            text.AppendLine(line.Replace("‎", "", StringComparison.Ordinal));
        }

        return text.ToString();
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    private static string SignedMoney(long amount, AppLanguage language) =>
        amount > 0 ? (language == AppLanguage.Arabic ? "‎+‎" : "+") + Money.Format(amount, language) : Money.Format(amount, language);
}
