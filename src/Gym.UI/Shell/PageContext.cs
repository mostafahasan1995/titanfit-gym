using Gym.Client;
using Gym.Contracts;
using Gym.Domain;
using Gym.UI.Station;

namespace Gym.UI.Shell;

/// <summary>The logged-in staff member. Created at login, gone at logout. The token never leaves memory.</summary>
public sealed class UserSession(LoginResponse login)
{
    public StaffDto Staff { get; } = login.Staff;

    public string Token { get; } = login.Token;

    public DateTime ExpiresAt { get; } = login.ExpiresAt;

    public StaffRole? Role => Staff.Role;

    public bool IsAdmin => Staff.Role == StaffRole.Admin;
}

/// <summary>Everything a page needs. Passed to <see cref="ShellPage.Create"/>; one per login.</summary>
public sealed class PageContext
{
    public PageContext(AppDefinition app, IGymApi api, ILiveConnection live, UserSession session, StationSettings station, IShellUi ui, bool isSample)
    {
        App = app;
        Api = api;
        Live = live;
        Session = session;
        Station = station;
        Ui = ui;
        IsSample = isSample;
    }

    public AppDefinition App { get; }

    /// <summary>The server (or the in-memory sample API in the smoke screenshots).</summary>
    public IGymApi Api { get; }

    /// <summary>Live updates. Events come on a background thread: use <c>Dispatcher.InvokeAsync</c>.</summary>
    public ILiveConnection Live { get; }

    public UserSession Session { get; }

    /// <summary>This PC's settings (station name, camera, printer). Updated when the server settings are saved.</summary>
    public StationSettings Station { get; internal set; }

    /// <summary>
    /// Changes this PC's settings and saves station.json, e.g. <c>Context.SaveStation(s =&gt; s with { PrinterName = name })</c>
    /// for the printer or camera picker. <see cref="Station"/> has the new values at once. False when the file could not
    /// be written. Nothing is written in the smoke screenshots.
    /// </summary>
    public bool SaveStation(Func<StationSettings, StationSettings> change)
    {
        ArgumentNullException.ThrowIfNull(change);
        if (StationSaver is { } save)
        {
            return save(change);
        }

        Station = change(Station);
        return true;
    }

    /// <summary>Set by the window: applies a change to its settings and writes the file.</summary>
    internal Func<Func<StationSettings, StationSettings>, bool>? StationSaver { get; set; }

    /// <summary>The station name to send with check-ins, sales and shifts.</summary>
    public string StationName => string.IsNullOrWhiteSpace(Station.StationName) ? App.DefaultStationName : Station.StationName.Trim();

    /// <summary>Toasts, dialogs, busy overlay, error messages.</summary>
    public IShellUi Ui { get; }

    /// <summary>
    /// True in the smoke screenshots (sample data, no server, no hardware): do not open the camera, scanner or printer.
    /// </summary>
    public bool IsSample { get; }

    public AppLanguage Language => UiLanguage.Current;
}

/// <summary>Optional for a page view: the shell calls these.</summary>
public interface IShellPage
{
    /// <summary>
    /// Each time the page is shown (also the first time): load or refresh its data here, not in <c>Loaded</c>.
    /// The smoke screenshot waits for it, so sample data is on screen. Exceptions show an error toast.
    /// </summary>
    Task OnShownAsync(CancellationToken cancellationToken);

    /// <summary>After the EN / عربي switch: update text set from code (XAML bound with <c>{ui:Loc}</c> updates by itself).</summary>
    void OnLanguageChanged();
}
