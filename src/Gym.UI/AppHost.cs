using System.Windows;

namespace Gym.UI;

/// <summary>Shared startup for the WPF apps: normal run, or the CI screenshot smoke mode.</summary>
public static class AppHost
{
    public static int Run(Application app, IReadOnlyList<string> args, Func<AppLanguage, Window> createMainWindow)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(createMainWindow);

        if (SmokeScreenshot.IsRequested(args))
        {
            return SmokeScreenshot.Run(args, createMainWindow);
        }

        AppLanguage language;
        try
        {
            language = AppLanguages.FromArgs(args);
        }
        catch (ArgumentException)
        {
            language = AppLanguage.English;
        }

        return app.Run(createMainWindow(language));
    }
}
