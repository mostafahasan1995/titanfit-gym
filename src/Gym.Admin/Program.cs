using Gym.UI;

namespace Gym.Admin;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var app = new App();
        app.InitializeComponent();
        return AppHost.Run(app, args, language => new MainWindow(language));
    }
}
