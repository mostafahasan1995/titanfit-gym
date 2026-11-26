using System.Windows;
using Gym.UI.Shell;

namespace Gym.UI;

/// <summary>
/// Shared startup for the WPF apps. Normal run: station.json, login, then the app's pages.
/// <c>--smoke-screenshots &lt;dir&gt; --lang en|ar</c>: the CI screenshot mode (see <see cref="SmokeScreenshot"/>).
/// <c>--lang en|ar</c> alone overrides the language saved in station.json.
/// </summary>
public static class AppHost
{
    public static int Run(Application app, IReadOnlyList<string> args, AppDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(definition);
        ThemeAssist.Initialize();

        if (SmokeScreenshot.IsRequested(args))
        {
            return SmokeScreenshot.Run(app, args, definition);
        }

        AppLanguage? language;
        try
        {
            language = AppLanguages.FromArgs(args);
        }
        catch (ArgumentException)
        {
            language = null;
        }

        var window = new StationWindow(definition, StationServices.ForServer(), language);

        // An unexpected error in a screen shows as a red toast instead of closing the app.
        app.DispatcherUnhandledException += (_, e) =>
        {
            window.Overlay.ShowError(e.Exception);
            e.Handled = true;
        };
        return app.Run(window);
    }
}
