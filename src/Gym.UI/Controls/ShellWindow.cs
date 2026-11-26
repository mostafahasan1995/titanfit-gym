using System.Windows;

namespace Gym.UI.Controls;

/// <summary>Main window of a TITANFIT app: the dark theme around an <see cref="AppShell"/>.</summary>
public class ShellWindow : Window
{
    public const double DesignWidth = 1440;
    public const double DesignHeight = 900;

    public ShellWindow(ShellDefinition definition, AppLanguage language)
    {
        SetResourceReference(StyleProperty, "Window.TitanFit");
        SetResourceReference(IconProperty, "Image.Logo");
        Width = DesignWidth;
        Height = DesignHeight;
        MinWidth = 1024;
        MinHeight = 640;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        if (SystemParameters.WorkArea.Width < DesignWidth || SystemParameters.WorkArea.Height < DesignHeight)
        {
            WindowState = WindowState.Maximized;
        }

        Shell = new AppShell(definition, language);
        Shell.LanguageChanged += (_, _) => UpdateTitle();
        Content = Shell;
        UpdateTitle();
        DarkTitleBar.Apply(this);
    }

    public AppShell Shell { get; }

    private void UpdateTitle() => Title = $"TITANFIT · {Shell.Definition.AppName.For(Shell.CurrentLanguage)}";
}
