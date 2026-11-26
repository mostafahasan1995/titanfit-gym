using Gym.UI;
using Gym.UI.Controls;

namespace Gym.Reception;

public sealed class MainWindow(AppLanguage language) : ShellWindow(ReceptionShell.Definition, language)
{
}
