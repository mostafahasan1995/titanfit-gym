using System.Net;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Gym.Client;
using Gym.Contracts;
using Gym.UI.Station;

namespace Gym.UI.Shell;

/// <summary>
/// The one window of a TITANFIT app: login screen, then the <see cref="AppShell"/> with the app's pages, with the
/// <see cref="OverlayHost"/> (toasts, dialogs, busy) on top. Handles station.json, login and role check, logout,
/// a revoked login (401), server settings and the language switch.
/// </summary>
public sealed class StationWindow : Window
{
    public const double DesignWidth = 1440;
    public const double DesignHeight = 900;

    private readonly StationServices services;
    private readonly ContentControl viewHost;
    private IGymApi api;
    private ILiveConnection? live;
    private PageContext? context;

    public StationWindow(AppDefinition app, StationServices services, AppLanguage? language = null)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(services);
        App = app;
        this.services = services;
        Settings = services.Store.Load();
        UiLanguage.Current = language ?? Settings.Language;

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

        // The root carries the theme itself (not only the window), so it also renders on its own (smoke screenshots).
        viewHost = new ContentControl { Focusable = false, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Stretch };
        Overlay = new OverlayHost();
        Root = new Grid { UseLayoutRounding = true, SnapsToDevicePixels = true };
        Root.SetResourceReference(Panel.BackgroundProperty, "Brush.Background");
        Root.SetResourceReference(TextElement.ForegroundProperty, "Brush.Text");
        Root.SetResourceReference(TextElement.FontFamilyProperty, "Font.Body");
        TextElement.SetFontSize(Root, 14);
        TextOptions.SetTextFormattingMode(Root, TextFormattingMode.Ideal);
        NumberSubstitution.SetSubstitution(Root, NumberSubstitutionMethod.European);
        Root.Children.Add(viewHost);
        Root.Children.Add(Overlay);
        Content = Root;

        api = services.CreateApi(Settings);
        api.SessionExpired += OnSessionExpired;
        ApplyLanguageToRoot();
        DarkTitleBar.Apply(this);
        ContentRendered += OnFirstRendered;
        ShowLogin(null);
    }

    public AppDefinition App { get; }

    /// <summary>This PC's settings as loaded or last saved.</summary>
    public StationSettings Settings { get; private set; }

    public IGymApi Api => api;

    /// <summary>The logged-in staff member, or null on the login screen.</summary>
    public UserSession? Session => context?.Session;

    public OverlayHost Overlay { get; }

    /// <summary>Everything inside the window (rendered on its own by the smoke screenshots).</summary>
    public Grid Root { get; }

    /// <summary>The login screen while it is shown.</summary>
    public LoginView? Login { get; private set; }

    /// <summary>The shell after login.</summary>
    public AppShell? Shell { get; private set; }

    /// <summary>Logs in; null when it worked, otherwise the message for the login card (wrong login, wrong role).</summary>
    public async Task<LocalText?> LogInAsync(string username, string password)
    {
        LoginResponse response;
        try
        {
            response = await api.LoginAsync(username, password).ConfigureAwait(true);
        }
        catch (GymApiException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return CommonText.WrongLogin;
        }

        if (!AppRoles.CanUse(App.App, response.Staff.Role))
        {
            api.Logout();
            return AppRoles.DeniedMessage(App.App);
        }

        await StartSessionAsync(response, username).ConfigureAwait(true);
        return null;
    }

    /// <summary>First run (Admin app): creates the first admin and logs in as him.</summary>
    public async Task<LocalText?> SetupFirstAdminAsync(SetupAdminRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var response = await api.SetupAdminAsync(request).ConfigureAwait(true);
        await StartSessionAsync(response, request.Username).ConfigureAwait(true);
        return null;
    }

    /// <summary>Back to the login screen (Log out button, revoked login, changed server).</summary>
    public void ShowLogin(LocalText? notice)
    {
        Shell?.Close();
        Shell = null;
        context = null;
        StopLive();
        api.Logout();
        Overlay.CloseAll();
        Login = new LoginView(this, notice);
        viewHost.Content = Login;
        UpdateTitle();
        _ = Login.CheckServerAsync();
    }

    /// <summary>Opens the server settings dialog; saving a new server address goes back to the login screen.</summary>
    public async Task OpenServerSettingsAsync(bool firstRun)
    {
        if (Overlay.HasDialog)
        {
            return;
        }

        var view = new ServerSettingsView(services, Settings, App.DefaultStationName);
        var saved = await Overlay.ShowDialogAsync(new DialogRequest
        {
            Title = CommonText.ServerSettings,
            Message = firstRun ? CommonText.FirstRunText : null,
            Content = view,
            PrimaryText = CommonText.Save,
            SecondaryText = CommonText.Cancel,
            OnPrimary = view.TrySaveAsync,
            Width = 580,
        }).ConfigureAwait(true);
        if (!saved || view.Result is not { } updated)
        {
            return;
        }

        var serverChanged = updated.ServerUri != Settings.ServerUri;
        Settings = updated;
        if (serverChanged)
        {
            api.SessionExpired -= OnSessionExpired;
            api = services.CreateApi(Settings);
            api.SessionExpired += OnSessionExpired;
            ShowLogin(context is null ? null : CommonText.SignInAgainNewServer);
        }
        else
        {
            if (context is not null)
            {
                context.Station = Settings;
                Shell?.UpdateStation();
            }

            Login?.ApplyLanguage();
        }

        Overlay.Toast(CommonText.SettingsSaved);
    }

    /// <summary>The EN / عربي switch: flips the layout, updates every text and remembers the choice in station.json.</summary>
    public void SetLanguage(AppLanguage language)
    {
        if (UiLanguage.Current == language)
        {
            return;
        }

        UiLanguage.Current = language;
        ApplyLanguageToRoot();
        Login?.ApplyLanguage();
        Shell?.ApplyLanguage();
        UpdateTitle();
        Settings = Settings with { Language = language };
        if (!services.IsSample && services.Store.Exists)
        {
            services.Store.TrySave(Settings);
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        StopLive();
        base.OnClosed(e);
    }

    private async Task StartSessionAsync(LoginResponse response, string username)
    {
        if (AppRoles.RemembersUsername(App.App) && !services.IsSample)
        {
            Settings = Settings with { LastUsername = username };
            if (services.Store.Exists)
            {
                services.Store.TrySave(Settings);
            }
        }

        StopLive();
        live = services.CreateLive(api);
        live.StatusChanged += OnLiveStatusChanged;
        context = new PageContext(App, api, live, new UserSession(response), Settings, Overlay, services.IsSample)
        {
            StationSaver = SaveStationSettings,
        };
        var shell = new AppShell(App, context);
        shell.LogoutRequested += (_, _) => ShowLogin(null);
        shell.ServerSettingsRequested += async (_, _) => await OpenServerSettingsAsync(firstRun: false).ConfigureAwait(true);
        shell.LanguageRequested += (_, language) => SetLanguage(language);
        Login = null;
        Shell = shell;
        viewHost.Content = shell;
        shell.SetLiveStatus(live.Status);
        live.Connect();
        UpdateTitle();
        await shell.ShowPageAsync(App.Pages[0]).ConfigureAwait(true);
    }

    /// <summary>A page changed a station setting (printer, camera): keep it here too, so a later save does not undo it.</summary>
    private bool SaveStationSettings(Func<StationSettings, StationSettings> change)
    {
        Settings = change(Settings);
        if (context is not null)
        {
            context.Station = Settings;
            Shell?.UpdateStation();
        }

        return services.IsSample || services.Store.TrySave(Settings);
    }

    private void StopLive()
    {
        if (live is null)
        {
            return;
        }

        var old = live;
        live = null;
        old.StatusChanged -= OnLiveStatusChanged;
        _ = DisposeQuietlyAsync(old);
    }

    private static async Task DisposeQuietlyAsync(ILiveConnection connection)
    {
        try
        {
            await connection.StopAsync().ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Closing a connection that is already broken is fine.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    private void OnLiveStatusChanged(LiveStatus status) =>
        _ = Dispatcher.InvokeAsync(() => Shell?.SetLiveStatus(status));

    private void OnSessionExpired(object? sender, EventArgs e) =>
        _ = Dispatcher.InvokeAsync(() =>
        {
            if (context is not null)
            {
                ShowLogin(CommonText.SessionExpired);
            }
        });

    private async void OnFirstRendered(object? sender, EventArgs e)
    {
        ContentRendered -= OnFirstRendered;
        if (!services.IsSample && !services.Store.Exists)
        {
            await OpenServerSettingsAsync(firstRun: true).ConfigureAwait(true);
        }
    }

    private void ApplyLanguageToRoot()
    {
        var language = UiLanguage.Current;
        Root.FlowDirection = language.ToFlowDirection();
        Root.Language = language.ToXmlLanguage();
    }

    private void UpdateTitle() => Title = $"TITANFIT · {App.AppName.For(UiLanguage.Current)}";
}
