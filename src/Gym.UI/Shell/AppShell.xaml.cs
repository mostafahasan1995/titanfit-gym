using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using Gym.Client;

namespace Gym.UI.Shell;

/// <summary>
/// The shared TITANFIT window layout after login. The app's <see cref="AppDefinition.Pages"/> fill the sidebar;
/// each page view is built once per login (so a cart or a form keeps its state when you switch pages).
/// </summary>
public partial class AppShell : UserControl
{
    private readonly Dictionary<ShellPage, FrameworkElement> views = [];
    private readonly List<NavItem> navItems;
    private CancellationTokenSource? shownCancellation;
    private LiveStatus liveStatus = LiveStatus.Offline;

    internal AppShell(AppDefinition app, PageContext context)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(context);
        if (app.Pages.Count == 0)
        {
            throw new ArgumentException("An app needs at least one page.", nameof(app));
        }

        InitializeComponent();
        App = app;
        Context = context;
        navItems = app.Pages.Select((p, i) => new NavItem(i, p.Icon)).ToList();
        NavList.ItemsSource = navItems;
        ApplyLanguage();
    }

    internal event EventHandler? LogoutRequested;

    internal event EventHandler? ServerSettingsRequested;

    internal event EventHandler<AppLanguage>? LanguageRequested;

    public AppDefinition App { get; }

    public PageContext Context { get; }

    /// <summary>The page on screen (null before the first one, or while an extra page such as the control gallery is shown).</summary>
    public ShellPage? CurrentPage { get; private set; }

    /// <summary>The view of the page on screen.</summary>
    public FrameworkElement? CurrentView => PageHost.Content as FrameworkElement;

    /// <summary>
    /// Shows a page (building it on first use) and runs its <see cref="IShellPage.OnShownAsync"/>.
    /// Returns the error when the page failed to build or load (it is also shown as a toast), otherwise null.
    /// </summary>
    internal async Task<Exception?> ShowPageAsync(ShellPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        shownCancellation?.Cancel();
        shownCancellation?.Dispose();
        shownCancellation = new CancellationTokenSource();
        var token = shownCancellation.Token;

        CurrentPage = page;
        var index = IndexOf(page);
        for (var i = 0; i < navItems.Count; i++)
        {
            navItems[i].IsActive = i == index;
        }

        UpdateHeader();
        try
        {
            if (!views.TryGetValue(page, out var view))
            {
                view = page.Create(Context);
                views[page] = view;
            }

            PageHost.Content = view;
            if (view is IShellPage shown)
            {
                await shown.OnShownAsync(token).ConfigureAwait(true);
            }

            return null;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return null;
        }
#pragma warning disable CA1031 // A broken page must not take the app down: show the error and keep the shell.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            if (!ErrorText.IsSessionExpired(ex))
            {
                Context.Ui.Toast(ErrorText.PageFailed.Format(UiLanguage.Current, ErrorText.Describe(ex, UiLanguage.Current)), ToastKind.Error);
            }

            return ex;
        }
    }

    /// <summary>Updates every text after the language switch and tells the page views.</summary>
    internal void ApplyLanguage()
    {
        var language = UiLanguage.Current;
        AppNameText.Text = App.AppName.For(language);
        AutomationProperties.SetName(NavList, App.NavLabel.For(language));
        for (var i = 0; i < navItems.Count; i++)
        {
            navItems[i].Label = App.Pages[i].Label.For(language);
        }

        UpdateStation();
        BidiText.SetText(StaffText, Context.Session.Staff.FullName);
        RoleText.Text = AppRoles.RoleName(Context.Session.Role).For(language);
        LogoutButton.Content = CommonText.LogOut.For(language);
        ServerButton.ToolTip = CommonText.ServerSettings.For(language);
        AutomationProperties.SetName(ServerButton, CommonText.ServerSettings.For(language));
        AutomationProperties.SetName(LanguageGroup, CommonText.Language.For(language));
        EnglishButton.IsChecked = language == AppLanguage.English;
        ArabicButton.IsChecked = language == AppLanguage.Arabic;
        UpdateHeader();
        SetLiveStatus(liveStatus);

        foreach (var view in views.Values.OfType<IShellPage>())
        {
            try
            {
                view.OnLanguageChanged();
            }
#pragma warning disable CA1031 // One page's text update must not stop the others.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                Context.Ui.ShowError(ex);
            }
        }
    }

    /// <summary>Shows the station name from the settings (after the server settings were saved).</summary>
    internal void UpdateStation() => BidiText.SetText(StationText, Context.StationName);

    /// <summary>The status light in the header.</summary>
    internal void SetLiveStatus(LiveStatus status)
    {
        liveStatus = status;
        var (brush, text) = status switch
        {
            LiveStatus.Connected => ("Brush.Good", CommonText.LiveConnected),
            LiveStatus.Connecting => ("Brush.Warning", CommonText.LiveConnecting),
            LiveStatus.Reconnecting => ("Brush.Warning", CommonText.LiveReconnecting),
            _ => ("Brush.Bad", CommonText.LiveOffline),
        };
        LiveDot.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, brush);
        LiveText.Text = text.For(UiLanguage.Current);
    }

    internal void HideHeaderTitle(LocalText title)
    {
        CurrentPage = null;
        TitleText.Text = title.For(UiLanguage.Current);
    }

    /// <summary>Shows a view that is not in the sidebar (the smoke screenshots' control gallery).</summary>
    internal async Task<Exception?> ShowExtraAsync(LocalText title, FrameworkElement view)
    {
        foreach (var item in navItems)
        {
            item.IsActive = false;
        }

        HideHeaderTitle(title);
        PageHost.Content = view;
        try
        {
            if (view is IShellPage shown)
            {
                await shown.OnShownAsync(CancellationToken.None).ConfigureAwait(true);
            }

            return null;
        }
#pragma warning disable CA1031 // Reported to the caller.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return ex;
        }
    }

    /// <summary>Stops the page loading (logout).</summary>
    internal void Close()
    {
        shownCancellation?.Cancel();
        shownCancellation?.Dispose();
        shownCancellation = null;
    }

    private int IndexOf(ShellPage page)
    {
        for (var i = 0; i < App.Pages.Count; i++)
        {
            if (ReferenceEquals(App.Pages[i], page))
            {
                return i;
            }
        }

        return -1;
    }

    private void UpdateHeader()
    {
        var language = UiLanguage.Current;
        if (CurrentPage is { } page)
        {
            TitleText.Text = (page.Title ?? page.Label).For(language);
        }

        SubtitleText.Text = DateText.LongDate(DateTime.Today, language);
    }

    private async void OnNavChecked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: NavItem item } && !ReferenceEquals(App.Pages[item.Index], CurrentPage))
        {
            await ShowPageAsync(App.Pages[item.Index]).ConfigureAwait(true);
        }
    }

    private void OnLanguageChecked(object sender, RoutedEventArgs e)
    {
        var language = ReferenceEquals(sender, ArabicButton) ? AppLanguage.Arabic : AppLanguage.English;
        if (language != UiLanguage.Current)
        {
            LanguageRequested?.Invoke(this, language);
        }
    }

    private void OnLogoutClick(object sender, RoutedEventArgs e) => LogoutRequested?.Invoke(this, EventArgs.Empty);

    private void OnServerSettingsClick(object sender, RoutedEventArgs e) => ServerSettingsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>A sidebar entry.</summary>
    internal sealed class NavItem(int index, Geometry icon) : INotifyPropertyChanged
    {
        private string label = "";
        private bool isActive;

        public event PropertyChangedEventHandler? PropertyChanged;

        public int Index { get; } = index;

        public Geometry Icon { get; } = icon;

        public string Label
        {
            get => label;
            set
            {
                if (label != value)
                {
                    label = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
                }
            }
        }

        public bool IsActive
        {
            get => isActive;
            set
            {
                if (isActive != value)
                {
                    isActive = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
                }
            }
        }
    }
}
