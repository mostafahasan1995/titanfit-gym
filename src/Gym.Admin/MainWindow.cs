using Gym.UI;
using Gym.UI.Controls;

namespace Gym.Admin;

public sealed class MainWindow(AppLanguage language) : ShellWindow(AdminShell.Definition, language)
{
}
