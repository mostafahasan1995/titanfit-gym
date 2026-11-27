using System.ComponentModel;
using System.Printing;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using Gym.UI;

namespace Gym.Cafeteria.Receipts;

/// <summary>
/// Prints receipts and shift reports. The receipt printer model is not known yet (docs/PLAN.md, open item 2):
/// <see cref="WindowsReceiptPrinter"/> prints through any Windows printer driver; an ESC/POS printer that talks
/// raw bytes can be added later behind this interface (the rows of <see cref="ReceiptLayout"/> are plain text).
/// </summary>
public interface IReceiptPrinter
{
    /// <summary>The printers installed on this PC, by their Windows name.</summary>
    IReadOnlyList<string> GetPrinterNames();

    /// <summary>
    /// Prints one slip. <paramref name="printerName"/> null or empty = the Windows default printer.
    /// Throws <see cref="ReceiptPrintException"/> with a message for the staff.
    /// </summary>
    void Print(ReceiptLayout layout, string? printerName);
}

/// <summary>Printing failed. <see cref="Text"/> is the staff message.</summary>
public sealed class ReceiptPrintException(LocalText text, string? detail = null, Exception? inner = null)
    : Exception(text.En + (detail is null ? "" : " " + detail), inner)
{
    public LocalText Text { get; } = text;

    public string? Detail { get; } = detail;
}

/// <summary>The smoke screenshots and tests: no printer is touched, the slips are kept in <see cref="Printed"/>.</summary>
public sealed class PreviewReceiptPrinter : IReceiptPrinter
{
    public List<(ReceiptLayout Layout, string? PrinterName)> Printed { get; } = [];

    public IReadOnlyList<string> GetPrinterNames() => [];

    public void Print(ReceiptLayout layout, string? printerName)
    {
        ArgumentNullException.ThrowIfNull(layout);
        Printed.Add((layout, printerName));
    }
}

/// <summary>
/// Prints through a Windows printer driver (WPF printing: an XPS page with the <see cref="ReceiptView"/>).
/// On a roll printer (paper narrower than 100 mm) the page is as long as the receipt, so the driver can cut right
/// after it; on a normal printer the receipt sits at the top of the page.
/// </summary>
public sealed class WindowsReceiptPrinter : IReceiptPrinter
{
    private const double RollLimit = 100 / 25.4 * 96;

    public IReadOnlyList<string> GetPrinterNames()
    {
        try
        {
            using var server = new LocalPrintServer();
            using var queues = server.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });
            var names = new List<string>();
            foreach (var queue in queues)
            {
                using (queue)
                {
                    names.Add(queue.FullName);
                }
            }

            return names.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();
        }
        catch (Exception ex) when (ex is PrintSystemException or Win32Exception or InvalidOperationException)
        {
            return [];
        }
    }

    public void Print(ReceiptLayout layout, string? printerName)
    {
        ArgumentNullException.ThrowIfNull(layout);
        try
        {
            using var queue = OpenQueue(printerName);
            var ticket = queue.UserPrintTicket ?? queue.DefaultPrintTicket ?? new PrintTicket();
            PageImageableArea? area = null;
            try
            {
                area = queue.GetPrintCapabilities(ticket).PageImageableArea;
            }
            catch (PrintQueueException)
            {
                // Some drivers do not report their printable area; use the receipt defaults.
            }

            var mediaWidth = ticket.PageMediaSize?.Width ?? 0;
            if (mediaWidth <= 0)
            {
                mediaWidth = ReceiptView.PaperWidth;
            }

            var isRoll = mediaWidth < RollLimit;
            var printable = area is { ExtentWidth: > 0 } ? area.ExtentWidth : mediaWidth - 30;
            var contentWidth = Math.Clamp(printable, 120, ReceiptView.PrintWidth);
            var left = area?.OriginWidth ?? Math.Max(0, (mediaWidth - contentWidth) / 2);
            var top = area?.OriginHeight ?? 0;

            var receipt = ReceiptView.Create(layout, contentWidth);
            receipt.Measure(new Size(contentWidth, double.PositiveInfinity));
            var mediaHeight = ticket.PageMediaSize?.Height ?? 0;
            var pageHeight = isRoll || mediaHeight <= 0
                ? Math.Ceiling(top + receipt.DesiredSize.Height + 24)
                : mediaHeight;
            var size = new Size(mediaWidth, pageHeight);

            var page = new FixedPage { Width = size.Width, Height = size.Height, Background = Brushes.White };
            FixedPage.SetLeft(receipt, left);
            FixedPage.SetTop(receipt, top);
            page.Children.Add(receipt);
            page.Measure(size);
            page.Arrange(new Rect(size));
            page.UpdateLayout();

            var content = new PageContent();
            ((IAddChild)content).AddChild(page);
            var document = new FixedDocument();
            document.DocumentPaginator.PageSize = size;
            document.Pages.Add(content);
            if (isRoll)
            {
                ticket.PageMediaSize = new PageMediaSize(size.Width, size.Height);
            }

            PrintQueue.CreateXpsDocumentWriter(queue).Write(document, ticket);
        }
        catch (ReceiptPrintException)
        {
            throw;
        }
        catch (Exception ex) when (ex is PrintSystemException or Win32Exception or InvalidOperationException or ArgumentException or System.IO.IOException)
        {
            throw new ReceiptPrintException(CafeteriaText.PrintFailed, ex.Message, ex);
        }
    }

    /// <summary>The printer by its Windows name (local or a network connection), or the default printer.</summary>
    private static PrintQueue OpenQueue(string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            try
            {
                return LocalPrintServer.GetDefaultPrintQueue();
            }
            catch (Exception ex) when (ex is PrintSystemException or Win32Exception or InvalidOperationException)
            {
                throw new ReceiptPrintException(CafeteriaText.NoDefaultPrinter, null, ex);
            }
        }

        using var server = new LocalPrintServer();
        using var queues = server.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });
        PrintQueue? found = null;
        foreach (var queue in queues)
        {
            if (found is null && (string.Equals(queue.FullName, printerName, StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(queue.Name, printerName, StringComparison.OrdinalIgnoreCase)))
            {
                found = queue;
            }
            else
            {
                queue.Dispose();
            }
        }

        return found ?? throw new ReceiptPrintException(CafeteriaText.PrinterNotFound, printerName);
    }
}
