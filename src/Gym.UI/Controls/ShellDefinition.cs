using System.Windows.Media;

namespace Gym.UI.Controls;

/// <summary>What one app shows in the TITANFIT shell: its name, station, role and sidebar pages.</summary>
public sealed record ShellDefinition(
    LocalText AppName,
    LocalText NavLabel,
    LocalText Station,
    LocalText Role,
    IReadOnlyList<ShellPage> Pages);

/// <summary>A sidebar page. <paramref name="Title"/> is the header title when it differs from the label.</summary>
public sealed record ShellPage(Geometry Icon, LocalText Label, LocalText? Title = null);

/// <summary>A sidebar item as shown for the current language.</summary>
public sealed record ShellNavItem(int Index, string Label, Geometry Icon, bool IsActive);
