using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using Gym.UI;

namespace Gym.Cafeteria.Receipts;

/// <summary>
/// Draws a <see cref="ReceiptLayout"/> as black text on white paper, the same for the on-screen preview and for
/// printing (<see cref="WindowsReceiptPrinter"/>). Width in device-independent pixels (1/96 inch):
/// 80 mm paper = <see cref="PaperWidth"/>, its printable area about 72 mm = <see cref="PrintWidth"/>.
/// Arabic receipts flow right to left; data without Arabic letters (station, codes) stays left to right.
/// </summary>
public static class ReceiptView
{
    /// <summary>80 mm.</summary>
    public const double PaperWidth = 80 / 25.4 * 96;

    /// <summary>72 mm, the printable width of most 80 mm thermal printers.</summary>
    public const double PrintWidth = 72 / 25.4 * 96;

    private static readonly Brush Ink = Freeze(new SolidColorBrush(Color.FromRgb(0x11, 0x11, 0x11)));
    private static readonly Brush SoftInk = Freeze(new SolidColorBrush(Color.FromRgb(0x3A, 0x3A, 0x3A)));

    /// <summary>The receipt as one element <paramref name="width"/> wide (white background, its own fonts).</summary>
    public static Border Create(ReceiptLayout layout, double width = PrintWidth)
    {
        ArgumentNullException.ThrowIfNull(layout);
        var body = FindFont("Font.Body", "Segoe UI");
        var heading = FindFont("Font.Heading", "Segoe UI");
        var panel = new StackPanel();
        foreach (var row in layout.Rows)
        {
            panel.Children.Add(CreateRow(row, body, heading));
        }

        var paper = new Border
        {
            Width = width,
            Background = Brushes.White,
            Padding = new Thickness(6, 10, 6, 14),
            Child = panel,
            FlowDirection = layout.Language.ToFlowDirection(),
            UseLayoutRounding = true,
            SnapsToDevicePixels = true,
        };
        TextOptions.SetTextFormattingMode(paper, TextFormattingMode.Ideal);
        NumberSubstitution.SetSubstitution(paper, NumberSubstitutionMethod.European);
        paper.Language = layout.Language.ToXmlLanguage();
        return paper;
    }

    /// <summary>Every text on the receipt, top to bottom (tests and accessibility).</summary>
    public static IReadOnlyList<string> Texts(DependencyObject receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        var texts = new List<string>();
        Collect(receipt, texts);
        return texts;
    }

    private static void Collect(DependencyObject element, List<string> texts)
    {
        if (element is TextBlock { Text.Length: > 0 } block)
        {
            texts.Add(block.Text);
        }

        foreach (var child in LogicalTreeHelper.GetChildren(element).OfType<DependencyObject>())
        {
            Collect(child, texts);
        }
    }

    private static FrameworkElement CreateRow(ReceiptRow row, FontFamily body, FontFamily heading) => row.Kind switch
    {
        ReceiptRowKind.Brand => Brand(heading),
        ReceiptRowKind.Title => Centered(row.Text, body, 13, FontWeights.SemiBold, new Thickness(0, 2, 0, 4)),
        ReceiptRowKind.Separator => Separator(),
        ReceiptRowKind.Info => Pair(row, body, 11.5, FontWeights.Normal, FontWeights.SemiBold, SoftInk, new Thickness(0, 1, 0, 1)),
        ReceiptRowKind.Item => Item(row.Text, body),
        ReceiptRowKind.ItemDetail => Pair(row, body, 11.5, FontWeights.Normal, FontWeights.SemiBold, SoftInk, new Thickness(0, 0, 0, 4)),
        ReceiptRowKind.Total => Total(row, body, heading),
        ReceiptRowKind.Amount => Pair(row, body, 12, FontWeights.Normal, FontWeights.SemiBold, Ink, new Thickness(0, 1, 0, 1)),
        _ => Centered(row.Text, body, 11.5, FontWeights.Normal, new Thickness(0, 2, 0, 0)),
    };

    private static FrameworkElement Brand(FontFamily heading)
    {
        var text = new TextBlock
        {
            FontFamily = heading,
            FontSize = 24,
            FontWeight = FontWeights.ExtraBold,
            Foreground = Ink,
            HorizontalAlignment = HorizontalAlignment.Center,
            FlowDirection = FlowDirection.LeftToRight,
            Margin = new Thickness(0, 0, 0, 2),
        };
        text.Inlines.Add(new System.Windows.Documents.Run("TITAN"));
        text.Inlines.Add(new System.Windows.Documents.Run("FIT") { FontWeight = FontWeights.Bold });
        return text;
    }

    private static TextBlock Centered(string text, FontFamily font, double size, FontWeight weight, Thickness margin) => new()
    {
        Text = text,
        FontFamily = font,
        FontSize = size,
        FontWeight = weight,
        Foreground = Ink,
        TextAlignment = TextAlignment.Center,
        TextWrapping = TextWrapping.Wrap,
        HorizontalAlignment = HorizontalAlignment.Stretch,
        Margin = margin,
    };

    private static FrameworkElement Separator() => new Rectangle
    {
        Height = 1,
        Margin = new Thickness(0, 6, 0, 6),
        Stroke = Ink,
        StrokeThickness = 1,
        StrokeDashArray = new DoubleCollection { 3, 3 },
        HorizontalAlignment = HorizontalAlignment.Stretch,
        SnapsToDevicePixels = true,
    };

    private static TextBlock Item(string text, FontFamily font)
    {
        var block = new TextBlock
        {
            FontFamily = font,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Foreground = Ink,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Left,
            Margin = new Thickness(0, 3, 0, 0),
        };
        SetDataText(block, text);
        return block;
    }

    private static Grid Pair(ReceiptRow row, FontFamily font, double size, FontWeight labelWeight, FontWeight valueWeight, Brush labelInk, Thickness margin)
    {
        var grid = new Grid { Margin = margin };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var label = new TextBlock
        {
            FontFamily = font,
            FontSize = size,
            FontWeight = labelWeight,
            Foreground = labelInk,
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0),
        };
        SetDataText(label, row.Text);
        grid.Children.Add(label);
        if (row.Value is { } value)
        {
            var valueText = new TextBlock
            {
                FontFamily = font,
                FontSize = size,
                FontWeight = valueWeight,
                Foreground = Ink,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Right,
            };
            SetDataText(valueText, value);
            valueText.HorizontalAlignment = HorizontalAlignment.Right;
            Grid.SetColumn(valueText, 1);
            grid.Children.Add(valueText);
        }

        return grid;
    }

    private static Grid Total(ReceiptRow row, FontFamily body, FontFamily heading)
    {
        var grid = Pair(row, body, 15, FontWeights.Bold, FontWeights.Bold, Ink, new Thickness(0, 2, 0, 4));
        if (grid.Children.Count > 1 && grid.Children[1] is TextBlock value)
        {
            value.FontFamily = heading;
            value.FontSize = 17;
            value.FontWeight = FontWeights.ExtraBold;
        }

        return grid;
    }

    /// <summary>
    /// Text that has Arabic letters follows the receipt direction; other text (codes, Latin names, numbers) is
    /// shown left to right so "Cafeteria 1" does not turn into "1 Cafeteria" on an Arabic receipt.
    /// The alignment (start or end of the line) is set by the caller and stays on the receipt's side.
    /// </summary>
    private static void SetDataText(TextBlock block, string text)
    {
        block.Text = text;
        if (!BidiText.HasRtl(text))
        {
            block.FlowDirection = FlowDirection.LeftToRight;
        }
    }

    private static FontFamily FindFont(string key, string fallback) =>
        Application.Current?.TryFindResource(key) as FontFamily ?? new FontFamily(fallback);

    private static Brush Freeze(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
