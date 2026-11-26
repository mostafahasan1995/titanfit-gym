using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Gym.UI;

/// <summary>
/// <c>--smoke-screenshot &lt;path.png&gt; [--lang en|ar]</c>: builds the main window, renders its content
/// offscreen at 1440 x 900 (no visible window, so it works on a headless CI runner), saves a PNG and exits.
/// Exit code 0 = saved, 1 = failed, 2 = bad arguments.
/// </summary>
public static class SmokeScreenshot
{
    public const string Flag = "--smoke-screenshot";
    public const int Width = 1440;
    public const int Height = 900;

    public static bool IsRequested(IReadOnlyList<string> args) =>
        args.Any(a => string.Equals(a, Flag, StringComparison.OrdinalIgnoreCase));

    public static int Run(IReadOnlyList<string> args, Func<AppLanguage, Window> createMainWindow)
    {
        ArgumentNullException.ThrowIfNull(createMainWindow);

        string outputPath;
        AppLanguage language;
        try
        {
            outputPath = ReadOutputPath(args);
            language = AppLanguages.FromArgs(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Smoke screenshot: {ex.Message}");
            return 2;
        }

        try
        {
            VerifyEmbeddedFont("Font.Heading", FontWeights.Bold);
            VerifyEmbeddedFont("Font.Body", FontWeights.Normal);
            VerifyEmbeddedFont("Font.Body", FontWeights.SemiBold);

            var window = createMainWindow(language);
            Save(window, outputPath);
            Console.WriteLine($"Saved {Width}x{Height} {language} screenshot: {outputPath}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Smoke screenshot failed: {ex}");
            return 1;
        }
    }

    private static string ReadOutputPath(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], Flag, StringComparison.OrdinalIgnoreCase))
            {
                var path = i + 1 < args.Count ? args[i + 1] : null;
                if (string.IsNullOrWhiteSpace(path) || path.StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"{Flag} needs an output path, e.g. {Flag} shot.png");
                }

                if (!path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException($"The output must be a .png file: {path}");
                }

                return Path.GetFullPath(path);
            }
        }

        throw new ArgumentException($"Missing {Flag} <path.png>.");
    }

    private static void Save(Window window, string outputPath)
    {
        if (window.Content is not UIElement content)
        {
            throw new InvalidOperationException("The main window has no content to render.");
        }

        // Render the window's content on its own: the window itself is never shown.
        window.Content = null;
        var host = new Border { Width = Width, Height = Height, Child = content };
        Layout(host);
        // Let queued work (bindings, item containers) run, then lay out again.
        host.Dispatcher.Invoke(static () => { }, DispatcherPriority.ContextIdle);
        Layout(host);

        var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }

    private static void Layout(FrameworkElement element)
    {
        element.Measure(new Size(Width, Height));
        element.Arrange(new Rect(0, 0, Width, Height));
        element.UpdateLayout();
    }

    /// <summary>Fails when an embedded font did not load (WPF would silently fall back to a system font).</summary>
    private static void VerifyEmbeddedFont(string resourceKey, FontWeight weight)
    {
        var family = (FontFamily)Application.Current.FindResource(resourceKey);
        var typeface = new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal);
        if (!typeface.TryGetGlyphTypeface(out var glyphs))
        {
            throw new InvalidOperationException($"Font {resourceKey} ({weight}) could not be loaded.");
        }

        var uri = glyphs.FontUri.ToString();
        Console.WriteLine($"Font {resourceKey} {weight}: {uri}");
        if (!uri.Contains("Fonts/", StringComparison.OrdinalIgnoreCase) || uri.Contains("Windows", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Font {resourceKey} ({weight}) fell back to a system font: {uri}");
        }
    }
}
