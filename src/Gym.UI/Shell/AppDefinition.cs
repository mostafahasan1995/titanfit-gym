using System.Windows;
using System.Windows.Media;
using Gym.Client.Sample;
using Gym.Domain;

namespace Gym.UI.Shell;

/// <summary>The three TITANFIT apps.</summary>
public enum GymApp
{
    Admin,
    Reception,
    Cafeteria,
}

/// <summary>Who may log in to which app (docs/PLAN.md, "App foundation").</summary>
public static class AppRoles
{
    /// <summary>Reception: Reception + Admin. Cafeteria: Cashier + Admin. Admin: Admin only.</summary>
    public static IReadOnlyList<StaffRole> Allowed(GymApp app) => app switch
    {
        GymApp.Reception => [StaffRole.Reception, StaffRole.Admin],
        GymApp.Cafeteria => [StaffRole.Cashier, StaffRole.Admin],
        GymApp.Admin => [StaffRole.Admin],
        _ => [],
    };

    public static bool CanUse(GymApp app, StaffRole? role) => role is { } r && Allowed(app).Contains(r);

    /// <summary>The message for a login whose role may not use the app.</summary>
    public static LocalText DeniedMessage(GymApp app) => app switch
    {
        GymApp.Reception => CommonText.ReceptionOnly,
        GymApp.Cafeteria => CommonText.CafeteriaOnly,
        _ => CommonText.AdminOnly,
    };

    /// <summary>
    /// The login screen fills in the last username on station PCs. Not in the Admin app: on the server PC the admin
    /// account stays logged out, and nothing about it is remembered.
    /// </summary>
    public static bool RemembersUsername(GymApp app) => app != GymApp.Admin;

    /// <summary>The role name shown under the staff name.</summary>
    public static LocalText RoleName(StaffRole? role) => role switch
    {
        StaffRole.Admin => CommonText.RoleAdmin,
        StaffRole.Reception => CommonText.RoleReception,
        StaffRole.Cashier => CommonText.RoleCashier,
        _ => CommonText.NotSignedIn,
    };
}

/// <summary>
/// What one app registers with the shared shell: its name, default station name and sidebar pages.
/// Each app has exactly one, e.g. <c>Gym.Reception/ReceptionApp.cs</c>.
/// </summary>
public sealed class AppDefinition
{
    public required GymApp App { get; init; }

    public required LocalText AppName { get; init; }

    /// <summary>Accessible name of the sidebar, e.g. "Reception menu".</summary>
    public required LocalText NavLabel { get; init; }

    /// <summary>Used when station.json has no station name, e.g. "Reception 1".</summary>
    public required string DefaultStationName { get; init; }

    /// <summary>The sidebar pages, top to bottom. The first one opens after login.</summary>
    public required IReadOnlyList<ShellPage> Pages { get; init; }

    /// <summary>
    /// Optional: changes or adds sample data for the smoke screenshots (e.g. call <c>CreateSaleAsync</c> on it).
    /// Runs once after the built-in sample data is created and before the first page is shown.
    /// </summary>
    public Func<SampleGymApi, Task>? SampleSetup { get; init; }

    /// <summary>
    /// Optional, smoke screenshots only: more states of a page (a filled-in form, another check-in result). Each is
    /// shown and captured after the pages as <c>&lt;App&gt;-&lt;Id&gt;-&lt;lang&gt;.png</c>.
    /// </summary>
    public IReadOnlyList<SmokeState> SmokeStates { get; init; } = [];

    /// <summary>
    /// The app's own <c>--self-test</c> checks (its native libraries and critical dependencies; no hardware), run after
    /// the shared font and rendering checks. See <see cref="SelfTest"/>.
    /// </summary>
    public IReadOnlyList<SelfTestCheck> SelfTestChecks { get; init; } = [];

    /// <summary>
    /// The window size drawn at 100 %; bigger windows draw the UI larger (see <see cref="UiScale"/>).
    /// <see cref="UiScale.TouchReference"/> makes a touch app (Reception) grow earlier.
    /// </summary>
    public Size ScaleReference { get; init; } = UiScale.DefaultReference;

    /// <summary>"Reception": the prefix of the screenshot files.</summary>
    public string Id => App.ToString();
}

/// <summary>
/// A sidebar page. <paramref name="Id"/> is short and stable ("check-in"): it names the screenshot file
/// (<c>Reception-check-in-en.png</c>). <paramref name="Create"/> builds the page view once per login.
/// </summary>
public sealed record ShellPage(string Id, Geometry Icon, LocalText Label, Func<PageContext, FrameworkElement> Create)
{
    /// <summary>The header title when it differs from the sidebar label.</summary>
    public LocalText? Title { get; init; }
}

/// <summary>
/// A smoke-screenshot-only state of a page: the shell shows the page <paramref name="PageId"/> (running its
/// <see cref="IShellPage.OnShownAsync"/>), then <paramref name="Prepare"/> gets its view to put it in the state
/// (e.g. fill in the form). The screenshot is <c>&lt;App&gt;-&lt;Id&gt;-&lt;lang&gt;.png</c>.
/// </summary>
public sealed record SmokeState(string Id, string PageId, Func<FrameworkElement, Task> Prepare);
