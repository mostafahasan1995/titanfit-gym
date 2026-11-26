using Gym.UI;
using Gym.UI.Controls;

namespace Gym.Cafeteria;

public sealed class MainWindow(AppLanguage language) : ShellWindow(CafeteriaShell.Definition, language)
{
}
