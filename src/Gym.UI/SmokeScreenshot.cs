using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Gym.Client.Sample;
using Gym.UI.Shell;

namespace Gym.UI;

/// <summary>
/// <c>--smoke-screenshots &lt;dir&gt; [--lang en|ar]</c>: runs the app on <see cref="SampleGymApi"/> (no server),
/// renders the login screen, the server settings dialog, every page of the app (and its extra smoke states), the control gallery and a dialog
/// offscreen at 1440 x 900, saves <c>&lt;dir&gt;/&lt;App&gt;-&lt;page&gt;-&lt;lang&gt;.png</c> and a manifest
/// <c>&lt;dir&gt;/&lt;App&gt;-&lt;lang&gt;.json</c>, and exits. A page that throws, a missing translation or a font that
/// did not load fails the run. Exit code 0 = all fine, 1 = something failed, 2 = bad arguments.
/// CI reads the manifest, so a new page gets its screenshot without a workflow change.
/// </summary>
public static class SmokeScreenshot
{
    public const string Flag = "--smoke-screenshots";
    public const int Width = 1440;
    public const int Height = 900;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static bool IsRequested(IReadOnlyList<string> args) =>
        args.Any(a => string.Equals(a, Flag, StringComparison.OrdinalIgnoreCase));

    /// <summary>The manifest file name for an app and language: <c>Reception-en.json</c>.</summary>
    public static string ManifestName(AppDefinition definition, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return $"{definition.Id}-{language.ToCode()}.json";
    }

    /// <summary>The screenshot file name of a page: <c>Reception-check-in-en.png</c>.</summary>
    public static string FileName(AppDefinition definition, string name, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return $"{definition.Id}-{name}-{language.ToCode()}.png";
    }

    public static int Run(Application app, IReadOnlyList<string> args, AppDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(definition);

        string folder;
        AppLanguage language;
        try
        {
            folder = ReadOutputFolder(args);
            language = AppLanguages.FromArgs(args) ?? AppLanguage.English;
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine($"Smoke screenshots: {ex.Message}");
            return 2;
        }

        var unhandled = new List<string>();
        app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        app.DispatcherUnhandledException += (_, e) =>
        {
            Console.Error.WriteLine($"Unhandled UI error: {e.Exception}");
            unhandled.Add(e.Exception.GetType().Name + ": " + e.Exception.Message);
            e.Handled = true;
        };
        app.Startup += async (_, _) =>
        {
            var exitCode = 1;
            try
            {
                exitCode = await RunAsync(definition, folder, language, unhandled).ConfigureAwait(true);
            }
#pragma warning disable CA1031 // Report every failure as a failed run.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Console.Error.WriteLine($"Smoke screenshots failed: {ex}");
            }
            finally
            {
                app.Shutdown(exitCode);
            }
        };
        return app.Run();
    }

    private static async Task<int> RunAsync(AppDefinition definition, string folder, AppLanguage language, List<string> unhandled)
    {
        VerifyEmbeddedFont("Font.Heading", FontWeights.Bold);
        VerifyEmbeddedFont("Font.Body", FontWeights.Normal);
        VerifyEmbeddedFont("Font.Body", FontWeights.SemiBold);

        var assemblies = new[] { Assembly.GetEntryAssembly(), typeof(CommonText).Assembly }.OfType<Assembly>().Distinct();
        var textProblems = assemblies.SelectMany(LocalTextAudit.FindProblems).ToList();
        var texts = assemblies.Sum(a => LocalTextAudit.FindTexts(a).Count);
        Console.WriteLine($"Strings checked: {texts}, problems: {textProblems.Count}");
        textProblems.ForEach(p => Console.Error.WriteLine("  Text: " + p));

        var bindings = new BindingErrorListener();
        PresentationTraceSources.Refresh();
        PresentationTraceSources.DataBindingSource.Listeners.Add(bindings);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;

        var sample = new SampleGymApi();
        if (definition.SampleSetup is { } setup)
        {
            await setup(sample).ConfigureAwait(true);
        }

        Directory.CreateDirectory(folder);
        var window = new StationWindow(definition, StationServices.ForSample(sample), language);
        var root = window.Root;
        window.Content = null;
        var host = new AdornerDecorator { Width = Width, Height = Height, Child = root };
        using var source = CreateHiddenSource(host);
        var shots = new List<SmokeShot>();

        async Task CaptureAsync(string name, Exception? error = null)
        {
            await SettleAsync(host).ConfigureAwait(true);
            var file = FileName(definition, name, language);
            Save(host, Path.Combine(folder, file));
            shots.Add(new SmokeShot(name, file, error is null, error?.ToString()));
            Console.WriteLine(error is null ? $"  OK   {file}" : $"  FAIL {file}: {error.GetType().Name}: {error.Message}");
            window.Overlay.CloseAll();
        }

        // Login, server settings and (Admin app) the first-run admin form.
        window.Login?.Prefill(SampleGymApi.UsernameFor(AppRoles.Allowed(definition.App)[0]), "");
        await CaptureAsync("login").ConfigureAwait(true);
        _ = window.OpenServerSettingsAsync(firstRun: true);
        await CaptureAsync("server-settings").ConfigureAwait(true);
        if (definition.App == GymApp.Admin)
        {
            window.Login?.ShowSetup();
            await CaptureAsync("first-admin").ConfigureAwait(true);
            window.ShowLogin(null);
        }

        // Log in with the app's first allowed role, then every page in the sidebar order.
        var username = SampleGymApi.UsernameFor(AppRoles.Allowed(definition.App)[0]);
        var refused = await window.LogInAsync(username, SampleGymApi.SamplePassword).ConfigureAwait(true);
        if (refused is { } message || window.Shell is not { } shell)
        {
            throw new InvalidOperationException($"The sample login as {username} failed: {refused?.En}");
        }

        foreach (var page in definition.Pages)
        {
            var error = await shell.ShowPageAsync(page).ConfigureAwait(true);
            await CaptureAsync(page.Id, error).ConfigureAwait(true);
        }

        // Extra states of the pages (AppDefinition.SmokeStates).
        foreach (var state in definition.SmokeStates)
        {
            var page = definition.Pages.FirstOrDefault(p => p.Id == state.PageId);
            var error = page is null
                ? new InvalidOperationException($"The smoke state {state.Id} names no page: {state.PageId}.")
                : await shell.ShowPageAsync(page).ConfigureAwait(true);
            if (error is null && shell.CurrentView is { } view)
            {
                try
                {
                    await SettleAsync(host).ConfigureAwait(true);
                    await state.Prepare(view).ConfigureAwait(true);
                }
#pragma warning disable CA1031 // Reported as a failed screenshot.
                catch (Exception ex)
#pragma warning restore CA1031
                {
                    error = ex;
                }
            }

            await CaptureAsync(state.Id, error).ConfigureAwait(true);
        }

        // The control gallery and a dialog over the first page.
        var galleryError = await shell.ShowExtraAsync(GalleryText.Title, new ControlsGalleryPage(shell.Context)).ConfigureAwait(true);
        await CaptureAsync("controls", galleryError).ConfigureAwait(true);
        var firstError = await shell.ShowPageAsync(definition.Pages[0]).ConfigureAwait(true);
        _ = window.Overlay.ConfirmAsync(CommonText.LogOut, CommonText.SessionExpired, CommonText.LogOut, danger: true);
        window.Overlay.Toast(CommonText.SettingsSaved);
        await CaptureAsync("dialog", firstError).ConfigureAwait(true);

        PresentationTraceSources.DataBindingSource.Listeners.Remove(bindings);
        var failed = shots.Count(s => !s.Ok) + textProblems.Count + unhandled.Count;
        var manifest = new SmokeManifest(definition.Id, language.ToCode(), failed == 0, shots, textProblems, unhandled, bindings.Messages);
        await File.WriteAllTextAsync(Path.Combine(folder, ManifestName(definition, language)), JsonSerializer.Serialize(manifest, Json)).ConfigureAwait(true);

        Console.WriteLine($"{definition.Id} ({language.ToCode()}): {shots.Count} screenshots, {shots.Count(s => !s.Ok)} failed pages, " +
                          $"{textProblems.Count} text problems, {unhandled.Count} unhandled errors, {bindings.Messages.Count} binding warnings.");
        foreach (var warning in bindings.Messages.Take(20))
        {
            Console.WriteLine("  Binding: " + warning);
        }

        window.Close();
        return failed == 0 ? 0 : 1;
    }

    private static string ReadOutputFolder(IReadOnlyList<string> args)
    {
        for (var i = 0; i < args.Count; i++)
        {
            if (string.Equals(args[i], Flag, StringComparison.OrdinalIgnoreCase))
            {
                var path = i + 1 < args.Count ? args[i + 1] : null;
                if (string.IsNullOrWhiteSpace(path) || path.StartsWith("--", StringComparison.Ordinal))
                {
                    throw new ArgumentException($"{Flag} needs an output folder, e.g. {Flag} screenshots");
                }

                return Path.GetFullPath(path);
            }
        }

        throw new ArgumentException($"Missing {Flag} <folder>.");
    }

    /// <summary>A hidden window for the content, so Loaded events, focus and layout work like in the real window.</summary>
    private static HwndSource? CreateHiddenSource(AdornerDecorator host)
    {
        try
        {
            const int WsPopup = unchecked((int)0x80000000);
            const int WsExToolWindow = 0x00000080;
            var source = new HwndSource(new HwndSourceParameters("TITANFIT smoke")
            {
                Width = Width,
                Height = Height,
                PositionX = -32000,
                PositionY = -32000,
                WindowStyle = WsPopup,
                ExtendedWindowStyle = WsExToolWindow,
            })
            {
                SizeToContent = SizeToContent.Manual,
                RootVisual = host,
            };
            return source;
        }
#pragma warning disable CA1031 // Without a window the content still renders; only Loaded events are missing.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Console.WriteLine($"No hidden window ({ex.Message}); rendering without one.");
            return null;
        }
    }

    /// <summary>Lets bindings, templates, Loaded handlers and queued work run, then lays out again.</summary>
    private static async Task SettleAsync(FrameworkElement host)
    {
        for (var i = 0; i < 3; i++)
        {
            host.Measure(new Size(Width, Height));
            host.Arrange(new Rect(0, 0, Width, Height));
            host.UpdateLayout();
            await Dispatcher.Yield(DispatcherPriority.ContextIdle);
        }
    }

    private static void Save(FrameworkElement host, string path)
    {
        var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void VerifyEmbeddedFont(string resourceKey, FontWeight weight) =>
        Console.WriteLine($"Font {resourceKey} {weight}: {EmbeddedFontUri(resourceKey, weight)}");

    /// <summary>
    /// The file of an embedded font; throws when it did not load (WPF would silently fall back to a system font).
    /// Also used by <see cref="SelfTest"/>.
    /// </summary>
    internal static string EmbeddedFontUri(string resourceKey, FontWeight weight)
    {
        var family = (FontFamily)Application.Current.FindResource(resourceKey);
        var typeface = new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal);
        if (!typeface.TryGetGlyphTypeface(out var glyphs))
        {
            throw new InvalidOperationException($"Font {resourceKey} ({weight}) could not be loaded.");
        }

        var uri = glyphs.FontUri.ToString();
        if (!uri.Contains("Fonts/", StringComparison.OrdinalIgnoreCase) || uri.Contains("Windows", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Font {resourceKey} ({weight}) fell back to a system font: {uri}");
        }

        return uri;
    }

    private sealed record SmokeShot(string Name, string File, bool Ok, string? Error);

    private sealed record SmokeManifest(
        string App,
        string Language,
        bool Ok,
        IReadOnlyList<SmokeShot> Screenshots,
        IReadOnlyList<string> TextProblems,
        IReadOnlyList<string> UnhandledErrors,
        IReadOnlyList<string> BindingWarnings);

    /// <summary>Collects WPF binding errors (printed as warnings; they do not fail the run).</summary>
    private sealed class BindingErrorListener : TraceListener
    {
        private readonly HashSet<string> seen = [];

        public List<string> Messages { get; } = [];

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message)
        {
            if (message is { Length: > 0 } && Messages.Count < 200 && seen.Add(message))
            {
                Messages.Add(message);
            }
        }
    }
}
