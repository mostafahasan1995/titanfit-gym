using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Xps.Packaging;
using Gym.Cafeteria.Receipts;
using Gym.UI;

namespace Gym.Cafeteria;

/// <summary>
/// The Cafeteria app's <c>--self-test</c> checks (see <see cref="SelfTest"/>), without a printer or scanner: the
/// receipt drawn in English and Arabic, and the receipt as the XPS print document Windows printing sends to a printer
/// driver (<see cref="WindowsReceiptPrinter"/>), written to a temporary file instead of a printer.
/// </summary>
public static class CafeteriaSelfTest
{
    public static IReadOnlyList<SelfTestCheck> Checks { get; } =
    [
        new("Receipt drawing (English and Arabic, offscreen)", ReceiptDrawing),
        new("Receipt print document (XPS, no printer)", ReceiptPrintDocument),
    ];

    public static string ReceiptDrawing()
    {
        var drawn = new List<string>();
        foreach (var language in new[] { AppLanguage.English, AppLanguage.Arabic })
        {
            var receipt = ReceiptView.Create(Sample(language));
            receipt.Measure(new Size(ReceiptView.PrintWidth, double.PositiveInfinity));
            receipt.Arrange(new Rect(receipt.DesiredSize));
            receipt.UpdateLayout();
            var width = (int)Math.Ceiling(receipt.ActualWidth);
            var height = (int)Math.Ceiling(receipt.ActualHeight);
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(receipt);
            var pixels = new byte[width * height * 4];
            bitmap.CopyPixels(pixels, width * 4, 0);
            var ink = 0;
            for (var i = 0; i < pixels.Length; i += 4)
            {
                if (pixels[i] < 128)
                {
                    ink++;
                }
            }

            if (ink < 500)
            {
                throw new InvalidOperationException($"The {language.ToCode()} receipt did not render ({ink} dark pixels in {width}x{height}).");
            }

            drawn.Add($"{language.ToCode()} {width}x{height}");
        }

        return string.Join(", ", drawn);
    }

    public static string ReceiptPrintDocument()
    {
        var receipt = ReceiptView.Create(Sample(AppLanguage.Arabic));
        receipt.Measure(new Size(ReceiptView.PrintWidth, double.PositiveInfinity));
        var size = new Size(ReceiptView.PaperWidth, Math.Ceiling(receipt.DesiredSize.Height + 24));
        var page = new FixedPage { Width = size.Width, Height = size.Height, Background = Brushes.White };
        FixedPage.SetLeft(receipt, (ReceiptView.PaperWidth - ReceiptView.PrintWidth) / 2);
        page.Children.Add(receipt);
        page.Measure(size);
        page.Arrange(new Rect(size));
        page.UpdateLayout();
        var content = new PageContent();
        ((IAddChild)content).AddChild(page);
        var document = new FixedDocument();
        document.DocumentPaginator.PageSize = size;
        document.Pages.Add(content);

        var path = Path.Combine(Path.GetTempPath(), $"titanfit-self-test-{Guid.NewGuid():N}.xps");
        try
        {
            using (var xps = new XpsDocument(path, FileAccess.ReadWrite))
            {
                var writer = XpsDocument.CreateXpsDocumentWriter(xps);
                writer.Write(document);
            }

            var length = new FileInfo(path).Length;
            var printing = typeof(System.Windows.Xps.XpsDocumentWriter).Assembly.GetName();
            return length > 2_000
                ? $"1 page, {length} bytes, {printing.Name} {printing.Version}"
                : throw new InvalidOperationException($"The XPS document is only {length} bytes.");
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static ReceiptLayout Sample(AppLanguage language) =>
        ReceiptLayout.TestPage("Self-test", "Cafeteria", language, DateTime.UtcNow);
}
