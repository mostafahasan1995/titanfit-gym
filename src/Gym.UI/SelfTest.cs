using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Gym.UI.Shell;

namespace Gym.UI;

/// <summary>One <c>--self-test</c> check: returns a short detail for its PASS line and throws when it fails.</summary>
public sealed record SelfTestCheck(string Name, Func<string> Run);

/// <summary>
/// <c>Gym.&lt;App&gt;.exe --self-test</c>: loads the app's native and critical libraries and exits. Checks: the embedded
/// fonts, WPF rendering (offscreen), the app's own checks (<see cref="AppDefinition.SelfTestChecks"/>), then which
/// Visual C++ runtime DLLs the process loaded. It prints one line per check (<c>PASS  name: detail</c> or
/// <c>FAIL  name: error</c>) and a summary, and exits with 0 when every check passed, 1 otherwise. It opens no window
/// and touches no camera, scanner, printer, server or settings file.
/// The install smoke test runs it on the installed exes. On a gym PC, in a command prompt:
/// <c>start /wait "" "C:\Program Files\TitanFit\Reception\Gym.Reception.exe" --self-test</c> (the lines appear in
/// that window), or in PowerShell: <c>&amp; 'C:\Program Files\TitanFit\Reception\Gym.Reception.exe' --self-test | Out-Host</c>.
/// </summary>
public static partial class SelfTest
{
    public const string Flag = "--self-test";

    private const int StdOutputHandle = -11;
    private const int AttachParentProcess = -1;

    public static bool IsRequested(IReadOnlyList<string> args) =>
        args.Any(a => string.Equals(a, Flag, StringComparison.OrdinalIgnoreCase));

    /// <summary>The app's self-test: the shared checks, the app's checks, then the Visual C++ runtime in use.</summary>
    public static int Run(AppDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        UseParentConsole();
        var checks = new List<SelfTestCheck>
        {
            new("Embedded fonts", EmbeddedFonts),
            new("WPF rendering (offscreen)", WpfRendering),
            new("Per-monitor DPI awareness", PerMonitorDpi),
        };
        checks.AddRange(definition.SelfTestChecks);
        checks.Add(new("Visual C++ runtime in use", LoadedVcRuntime));
        var version = Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3) ?? "?";
        return Run($"TITANFIT {definition.Id} {version}", checks, Console.Out);
    }

    /// <summary>Runs the checks in order, one line each, then the summary. 0 = all passed, 1 = at least one failed.</summary>
    public static int Run(string title, IReadOnlyList<SelfTestCheck> checks, TextWriter output)
    {
        ArgumentNullException.ThrowIfNull(checks);
        ArgumentNullException.ThrowIfNull(output);
        var failed = 0;
        foreach (var check in checks)
        {
            string line;
            try
            {
                line = $"PASS  {check.Name}: {OneLine(check.Run())}";
            }
#pragma warning disable CA1031 // Every failure is a FAIL line; the other checks still run.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                failed++;
                line = $"FAIL  {check.Name}: {Describe(ex)}";
            }

            output.WriteLine(line);
        }

        output.WriteLine(failed == 0
            ? $"{title} self-test: all {checks.Count} checks passed."
            : $"{title} self-test: {failed} of {checks.Count} checks FAILED.");
        output.Flush();
        return failed == 0 ? 0 : 1;
    }

    /// <summary>
    /// The error of a failed check on one line. A native library that does not load shows up wrapped
    /// (TypeInitializationException around a DllNotFoundException): the inner error is the one that matters.
    /// </summary>
    public static string Describe(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        while (error is TypeInitializationException or TargetInvocationException or AggregateException
               && error.InnerException is { } inner)
        {
            error = inner;
        }

        var text = $"{error.GetType().Name}: {error.Message}";
        if (error.InnerException is { } cause && cause.Message != error.Message)
        {
            text += $" ({cause.GetType().Name}: {cause.Message})";
        }

        return OneLine(text);
    }

    /// <summary>The file version of a DLL loaded in this process, e.g. "libSkiaSharp.dll 3.119.2.0"; throws when it is not loaded.</summary>
    public static string LoadedModule(string fileName)
    {
        using var process = Process.GetCurrentProcess();
        var module = process.Modules.Cast<ProcessModule>()
            .FirstOrDefault(m => string.Equals(m.ModuleName, fileName, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"{fileName} is not loaded.");
        var version = module.FileVersionInfo.FileVersion;
        return string.IsNullOrWhiteSpace(version) ? $"{fileName} loaded" : $"{fileName} {version.Trim()}";
    }

    private static string EmbeddedFonts()
    {
        var files = new[]
        {
            SmokeScreenshot.EmbeddedFontUri("Font.Heading", FontWeights.Bold),
            SmokeScreenshot.EmbeddedFontUri("Font.Body", FontWeights.Normal),
            SmokeScreenshot.EmbeddedFontUri("Font.Body", FontWeights.SemiBold),
        };
        return string.Join(", ", files.Select(uri => uri[(uri.LastIndexOf('/') + 1)..]));
    }

    /// <summary>English and Arabic text in the app font, drawn by WPF's own native libraries (no window).</summary>
    private static string WpfRendering()
    {
        var text = new TextBlock
        {
            Text = "TITANFIT \u0646\u0627\u062F\u064A 0123",
            FontFamily = Application.Current?.TryFindResource("Font.Body") as FontFamily ?? new FontFamily("Segoe UI"),
            FontSize = 28,
            Foreground = Brushes.Black,
        };
        var paper = new Border { Background = Brushes.White, Padding = new Thickness(8), Child = text };
        paper.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        paper.Arrange(new Rect(paper.DesiredSize));
        paper.UpdateLayout();
        var width = (int)Math.Ceiling(paper.ActualWidth);
        var height = (int)Math.Ceiling(paper.ActualHeight);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(paper);
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

        if (ink < 100)
        {
            throw new InvalidOperationException($"The text did not render ({ink} dark pixels in {width}x{height}).");
        }

        return $"{width}x{height} English + Arabic text, {ink} dark pixels, render tier {RenderCapability.Tier >> 16}";
    }

    /// <summary>The process runs per-monitor V2 DPI aware (app.manifest), so the UI is sharp at any display scaling.</summary>
    private static string PerMonitorDpi()
    {
        var awareness = DpiAwareness.Current();
        return awareness == "PerMonitorV2"
            ? awareness
            : throw new InvalidOperationException($"The process is {awareness} DPI aware, not PerMonitorV2 (check app.manifest).");
    }

    /// <summary>
    /// The Microsoft Visual C++ runtime DLLs this process loaded (after the app's checks loaded its native libraries).
    /// Informational: "none" means nothing loaded so far needs it. WPF's own copy (vcruntime140_cor3.dll) does not count.
    /// </summary>
    private static string LoadedVcRuntime()
    {
        using var process = Process.GetCurrentProcess();
        var loaded = process.Modules.Cast<ProcessModule>()
            .Where(m => VcRuntimeDll().IsMatch(m.ModuleName ?? ""))
            .Select(m => $"{m.ModuleName} {m.FileVersionInfo.FileVersion?.Trim()} ({Path.GetDirectoryName(m.FileName)})")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return loaded.Count == 0 ? "none (nothing loaded needs it)" : string.Join(", ", loaded);
    }

    private static string OneLine(string? text) =>
        string.Join(' ', (text ?? "").Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>
    /// A WinExe has no console. Started from a command prompt without redirection, write to that prompt's window;
    /// with redirected output (the install smoke test, a pipe) the lines go there as usual.
    /// </summary>
    private static void UseParentConsole()
    {
        var handle = GetStdHandle(StdOutputHandle);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            _ = AttachConsole(AttachParentProcess);
        }
    }

    [GeneratedRegex(@"^(msvcp140(_\d+|_atomic_wait|_codecvt_ids)?|vcruntime140(_1|_threads)?|concrt140|vcomp140)\.dll$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex VcRuntimeDll();

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern IntPtr GetStdHandle(int handle);

    [DllImport("kernel32.dll", ExactSpelling = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AttachConsole(int processId);
}
