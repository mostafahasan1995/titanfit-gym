using Gym.UI;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using LiveChartsCore.SkiaSharpView.SKCharts;
using SkiaSharp;

namespace Gym.Admin;

/// <summary>
/// The Admin app's <c>--self-test</c> checks (see <see cref="SelfTest"/>): the native libraries of the dashboard chart
/// (SkiaSharp and HarfBuzz) and a LiveCharts2 column chart like the dashboard's, drawn offscreen.
/// </summary>
public static class AdminSelfTest
{
    private static readonly SKColor Accent = new(0x22, 0xD8, 0xF0);

    public static IReadOnlyList<SelfTestCheck> Checks { get; } =
    [
        new("SkiaSharp native library", SkiaSharpLibrary),
        new("HarfBuzz native library (text shaping)", HarfBuzzLibrary),
        new("Dashboard chart (LiveCharts2 on SkiaSharp, offscreen)", DashboardChart),
    ];

    public static string SkiaSharpLibrary()
    {
        using var bitmap = new SKBitmap(64, 64);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);
            using var paint = new SKPaint { Color = Accent, IsAntialias = false };
            canvas.DrawRect(SKRect.Create(8, 8, 48, 48), paint);
        }

        var pixel = bitmap.GetPixel(32, 32);
        return pixel == Accent
            ? $"64x64 drawing, {SelfTest.LoadedModule("libSkiaSharp.dll")}"
            : throw new InvalidOperationException($"SkiaSharp drew {pixel} instead of {Accent}.");
    }

    public static string HarfBuzzLibrary()
    {
        using var buffer = new HarfBuzzSharp.Buffer();
        buffer.AddUtf16("نادي تايتن");
        buffer.GuessSegmentProperties();
        return buffer.Direction == HarfBuzzSharp.Direction.RightToLeft
            ? $"Arabic text is right to left, {SelfTest.LoadedModule("libHarfBuzzSharp.dll")}"
            : throw new InvalidOperationException($"HarfBuzz gave the direction {buffer.Direction} for Arabic text.");
    }

    /// <summary>Check-ins by hour as on the dashboard (columns in the accent colour, hour labels), drawn to a bitmap.</summary>
    public static string DashboardChart()
    {
        const int Width = 480;
        const int Height = 240;
        var chart = new SKCartesianChart
        {
            Width = Width,
            Height = Height,
            Background = new SKColor(0x0F, 0x27, 0x31),
            AnimationsSpeed = TimeSpan.Zero,
            Series = [new ColumnSeries<int> { Values = new[] { 3, 7, 12, 9, 4 }, Fill = new SolidColorPaint(Accent), MaxBarWidth = 26 }],
            XAxes = [new Axis { Labels = new[] { "16", "17", "18", "19", "20" }, LabelsPaint = new SolidColorPaint(SKColors.White), TextSize = 12 }],
            YAxes = [new Axis { MinLimit = 0, LabelsPaint = new SolidColorPaint(SKColors.White), TextSize = 12 }],
        };
        using var bitmap = new SKBitmap(Width, Height);
        using (var canvas = new SKCanvas(bitmap))
        {
            chart.DrawOnCanvas(canvas);
        }

        var columns = bitmap.Pixels.Count(p => p == Accent);
        return columns >= 500
            ? $"{Width}x{Height} column chart, {columns} column pixels"
            : throw new InvalidOperationException($"The chart has no columns ({columns} pixels in the column colour).");
    }
}
