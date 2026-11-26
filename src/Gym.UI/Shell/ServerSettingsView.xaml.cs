using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Gym.Client;
using Gym.UI.Station;

namespace Gym.UI.Shell;

/// <summary>The content of the "Server settings" dialog. <see cref="TrySaveAsync"/> is the dialog's Save.</summary>
public partial class ServerSettingsView : UserControl
{
    private readonly StationServices services;
    private readonly StationSettings current;
    private readonly string defaultStationName;

    internal ServerSettingsView(StationServices services, StationSettings current, string defaultStationName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(current);
        InitializeComponent();
        this.services = services;
        this.current = current;
        this.defaultStationName = defaultStationName;
        ServerBox.Text = current.ServerUrl;
        StationBox.TextChanged += (_, _) => FollowTextDirection(StationBox);
        StationBox.Text = string.IsNullOrWhiteSpace(current.StationName) ? defaultStationName : current.StationName;
        FollowTextDirection(StationBox);
        ApplyLanguage();
    }

    /// <summary>The saved settings after a successful <see cref="TrySaveAsync"/>.</summary>
    internal StationSettings? Result { get; private set; }

    private void ApplyLanguage()
    {
        var language = UiLanguage.Current;
        ServerField.Label = CommonText.ServerAddress.For(language);
        ServerField.Hint = CommonText.ServerAddressHint.For(language);
        StationField.Label = CommonText.StationName.For(language);
        StationField.Hint = CommonText.StationNameHint.For(language);
        TestButton.Content = CommonText.TestConnection.For(language);
        FileText.Text = CommonText.SettingsFile.Format(language, services.Store.FilePath);
        AutomationProperties.SetName(ServerBox, CommonText.ServerAddress.For(language));
        AutomationProperties.SetName(StationBox, CommonText.StationName.For(language));
        ThemeAssist.SetPlaceholder(ServerBox, StationSettings.DefaultServerUrl);
    }

    internal Task<bool> TrySaveAsync()
    {
        var language = UiLanguage.Current;
        if (!StationSettings.TryNormalizeServerUrl(ServerBox.Text, out var url))
        {
            ServerField.Error = CommonText.InvalidServerAddress.For(language);
            return Task.FromResult(false);
        }

        ServerField.Error = null;
        var station = StationBox.Text.Trim();
        var updated = current with
        {
            ServerUrl = url,
            StationName = station.Length == 0 || station == defaultStationName ? null : station,
            Language = UiLanguage.Current,
        };
        if (!services.IsSample)
        {
            try
            {
                services.Store.Save(updated);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                ShowResult(false, CommonText.SettingsSaveFailed.Format(language, ex.Message));
                return Task.FromResult(false);
            }
        }

        Result = updated;
        return Task.FromResult(true);
    }

    /// <summary>"Reception 1" stays in that order in the Arabic layout; an Arabic name stays right-to-left.</summary>
    private static void FollowTextDirection(TextBox box)
    {
        if (BidiText.HasRtl(box.Text))
        {
            box.ClearValue(FlowDirectionProperty);
        }
        else
        {
            box.FlowDirection = FlowDirection.LeftToRight;
        }
    }

    private async void OnTestClick(object sender, RoutedEventArgs e)
    {
        var language = UiLanguage.Current;
        if (!StationSettings.TryNormalizeServerUrl(ServerBox.Text, out var url))
        {
            ServerField.Error = CommonText.InvalidServerAddress.For(language);
            return;
        }

        ServerField.Error = null;
        ServerBox.Text = url;
        TestButton.IsEnabled = false;
        ShowResult(null, CommonText.Testing.For(language));
        try
        {
            var health = await services.TestServerAsync(new Uri(url + "/"), CancellationToken.None).ConfigureAwait(true);
            ShowResult(true, CommonText.ConnectionOk.Format(UiLanguage.Current, health.Version));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or GymApiException or InvalidOperationException
                                       or System.Text.Json.JsonException)
        {
            ShowResult(false, CommonText.ConnectionFailed.Format(UiLanguage.Current, ErrorText.Describe(ex, UiLanguage.Current)));
        }
        finally
        {
            TestButton.IsEnabled = true;
        }
    }

    private void ShowResult(bool? ok, string text)
    {
        TestResult.Visibility = Visibility.Visible;
        TestText.Text = text;
        var (brush, icon) = ok switch
        {
            true => ("Brush.Good", UiIcons.Check),
            false => ("Brush.Bad", UiIcons.Alert),
            null => ("Brush.TextSecondary", UiIcons.Clock),
        };
        TestText.SetResourceReference(TextBlock.ForegroundProperty, brush);
        TestIcon.SetResourceReference(Controls.Icon.ForegroundProperty, brush);
        TestIcon.Data = icon;
    }
}
