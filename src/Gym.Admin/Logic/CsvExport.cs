using System.Globalization;
using System.Text;
using Gym.Admin.Pages;
using Gym.UI;

namespace Gym.Admin.Logic;

/// <summary>
/// The CSV file of a report for Excel: UTF-8 with a byte order mark (so Excel shows Arabic), commas, CRLF lines,
/// fields quoted only when needed, numbers and money as plain digits, dates as yyyy-MM-dd.
/// Layout: a title line (report, from, to), the totals, then each table with its title and column headers.
/// </summary>
public static class CsvExport
{
    public const string LineBreak = "\r\n";

    private static readonly UTF8Encoding Utf8WithBom = new(encoderShouldEmitUTF8Identifier: true);
    private static readonly char[] Special = [',', '"', '\r', '\n', ';'];

    public static string Build(ReportDocument document, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(document);
        var csv = new StringBuilder();
        AppendLine(csv, [
            document.Title,
            ReportsText.From.For(language),
            document.From.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ReportsText.To.For(language),
            document.To.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        ]);
        csv.Append(LineBreak);
        AppendLine(csv, [ReportsText.Totals.For(language)]);
        foreach (var total in document.Totals)
        {
            AppendLine(csv, [total.Label, total.Raw]);
        }

        foreach (var table in document.Tables)
        {
            csv.Append(LineBreak);
            AppendLine(csv, [table.Title]);
            AppendLine(csv, table.Columns.Select(c => c.Header));
            foreach (var row in table.Rows)
            {
                AppendLine(csv, row.Select(c => c.Raw));
            }
        }

        return csv.ToString();
    }

    /// <summary>The file bytes: the UTF-8 byte order mark (EF BB BF) and the text.</summary>
    public static byte[] ToBytes(string csv)
    {
        ArgumentNullException.ThrowIfNull(csv);
        var preamble = Utf8WithBom.GetPreamble();
        var body = Utf8WithBom.GetBytes(csv);
        var bytes = new byte[preamble.Length + body.Length];
        preamble.CopyTo(bytes, 0);
        body.CopyTo(bytes, preamble.Length);
        return bytes;
    }

    /// <summary>One CSV line (without the line break).</summary>
    public static string Line(IEnumerable<string?> fields) => string.Join(",", fields.Select(Escape));

    /// <summary>Quotes a field with a comma, a quote, a line break or spaces at either end; quotes inside are doubled.</summary>
    public static string Escape(string? field)
    {
        field ??= "";
        var needsQuotes = field.Length > 0
            && (field.IndexOfAny(Special) >= 0 || char.IsWhiteSpace(field[0]) || char.IsWhiteSpace(field[^1]));
        return needsQuotes ? "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : field;
    }

    /// <summary>A suggested file name: titanfit-sales-20261101-20261127.csv.</summary>
    public static string FileName(ReportDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        return string.Create(CultureInfo.InvariantCulture, $"titanfit-{document.FileKey}-{document.From:yyyyMMdd}-{document.To:yyyyMMdd}.csv");
    }

    private static void AppendLine(StringBuilder csv, IEnumerable<string?> fields) => csv.Append(Line(fields)).Append(LineBreak);
}
