using Gym.UI;

namespace Gym.Reception;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var app = new App();
        app.InitializeComponent();
        return AppHost.Run(app, args, ReceptionApp.Definition);
    }
}
