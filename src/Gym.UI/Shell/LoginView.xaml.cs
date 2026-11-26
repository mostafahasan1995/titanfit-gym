using System.Net.Http;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using Gym.Client;
using Gym.Contracts;

namespace Gym.UI.Shell;

/// <summary>The login screen. Enter in the username moves to the password; Enter in the password signs in.</summary>
public partial class LoginView : UserControl
{
    private readonly StationWindow host;
    private Func<AppLanguage, string>? message;
    private bool busy;

    internal LoginView(StationWindow host, LocalText? notice)
    {
        ArgumentNullException.ThrowIfNull(host);
        InitializeComponent();
        this.host = host;
        if (AppRoles.RemembersUsername(host.App.App) && host.Settings.LastUsername is { Length: > 0 } last)
        {
            UsernameBox.Text = last;
        }

        if (notice is { } text)
        {
            ShowMessage(l => text.For(l), MessageTone.Info);
        }

        ApplyLanguage();
        Loaded += (_, _) => (UsernameBox.Text.Length > 0 ? (Control)PasswordBox : UsernameBox).Focus();
    }

    /// <summary>True while the "create the first admin" form is shown.</summary>
    internal bool IsSetupMode { get; private set; }

    internal void ApplyLanguage()
    {
        var language = UiLanguage.Current;
        AppNameText.Text = host.App.AppName.For(language);
        TitleText.Text = (IsSetupMode ? CommonText.FirstAdminTitle : CommonText.SignInTitle).For(language);
        SubtitleText.Text = (IsSetupMode ? CommonText.FirstAdminText : CommonText.SignInText).For(language);
        SetField(UsernameField, UsernameBox, CommonText.Username, language);
        SetField(PasswordField, PasswordBox, CommonText.Password, language);
        SetField(SetupNameField, SetupNameBox, CommonText.FullName, language);
        SetField(SetupUsernameField, SetupUsernameBox, CommonText.Username, language);
        SetField(SetupPasswordField, SetupPasswordBox, CommonText.Password, language);
        SetField(SetupConfirmField, SetupConfirmBox, CommonText.ConfirmPassword, language);
        SubmitButton.Content = busy
            ? CommonText.SigningIn.For(language)
            : (IsSetupMode ? CommonText.CreateAdmin : CommonText.SignIn).For(language);
        ServerCaption.Text = CommonText.ServerLabel.For(language);
        ServerText.Text = host.Settings.ServerUri.GetLeftPart(UriPartial.Authority);
        ServerButton.Content = CommonText.ServerSettings.For(language);
        AutomationProperties.SetName(LanguageGroup, CommonText.Language.For(language));
        EnglishButton.IsChecked = language == AppLanguage.English;
        ArabicButton.IsChecked = language == AppLanguage.Arabic;
        if (message is not null)
        {
            MessageText.Text = message(language);
        }
    }

    /// <summary>Asks the server whether it still needs its first admin, and says so when it cannot be reached.</summary>
    internal async Task CheckServerAsync()
    {
        try
        {
            var status = await host.Api.GetSetupStatusAsync().ConfigureAwait(true);
            if (!status.NeedsSetup)
            {
                return;
            }

            if (host.App.App == GymApp.Admin)
            {
                ShowSetup();
            }
            else
            {
                ShowMessage(l => CommonText.NoAdminYet.For(l), MessageTone.Warning);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or GymApiException)
        {
            ShowMessage(l => CommonText.ServerUnreachable.For(l), MessageTone.Warning);
        }
    }

    /// <summary>Switches to the "create the first admin" form (Admin app on a fresh server).</summary>
    internal void ShowSetup()
    {
        IsSetupMode = true;
        SignInForm.Visibility = Visibility.Collapsed;
        SetupForm.Visibility = Visibility.Visible;
        ClearMessage();
        ApplyLanguage();
    }

    /// <summary>Fills in the form (the smoke screenshots).</summary>
    internal void Prefill(string username, string password)
    {
        UsernameBox.Text = username;
        PasswordBox.Password = password;
    }

    private static void SetField(Controls.FormField field, Control input, LocalText label, AppLanguage language)
    {
        field.Label = label.For(language);
        AutomationProperties.SetName(input, label.For(language));
    }

    private void ShowMessage(Func<AppLanguage, string> text, MessageTone tone)
    {
        message = text;
        MessageText.Text = text(UiLanguage.Current);
        var (background, border, icon, geometry) = tone switch
        {
            MessageTone.Error => ("Brush.BadSoft", "Brush.BadLine", "Brush.Bad", UiIcons.Alert),
            MessageTone.Warning => ("Brush.WarningSoft", "Brush.WarningLine", "Brush.Warning", UiIcons.Alert),
            _ => ("Brush.AccentSoft", "Brush.AccentLine", "Brush.Accent", UiIcons.Info),
        };
        MessageBorder.SetResourceReference(Border.BackgroundProperty, background);
        MessageBorder.SetResourceReference(Border.BorderBrushProperty, border);
        MessageIcon.SetResourceReference(Controls.Icon.ForegroundProperty, icon);
        MessageIcon.Data = geometry;
        MessageBorder.Visibility = Visibility.Visible;
    }

    private void ClearMessage()
    {
        message = null;
        MessageBorder.Visibility = Visibility.Collapsed;
    }

    private async Task SubmitAsync()
    {
        if (busy)
        {
            return;
        }

        LocalText? problem = null;
        if (IsSetupMode)
        {
            if (SetupNameBox.Text.Trim().Length == 0 || SetupUsernameBox.Text.Trim().Length == 0 || SetupPasswordBox.Password.Length == 0)
            {
                problem = CommonText.FillAllFields;
            }
            else if (SetupPasswordBox.Password != SetupConfirmBox.Password)
            {
                problem = CommonText.PasswordsDiffer;
            }
        }
        else if (UsernameBox.Text.Trim().Length == 0 || PasswordBox.Password.Length == 0)
        {
            problem = CommonText.EnterUsernameAndPassword;
        }

        if (problem is { } text)
        {
            ShowMessage(l => text.For(l), MessageTone.Error);
            return;
        }

        busy = true;
        SubmitButton.IsEnabled = false;
        ApplyLanguage();
        try
        {
            var error = IsSetupMode
                ? await host.SetupFirstAdminAsync(new SetupAdminRequest(SetupNameBox.Text.Trim(), SetupUsernameBox.Text.Trim(), SetupPasswordBox.Password)).ConfigureAwait(true)
                : await host.LogInAsync(UsernameBox.Text.Trim(), PasswordBox.Password).ConfigureAwait(true);
            if (error is { } refused)
            {
                PasswordBox.Clear();
                ShowMessage(l => refused.For(l), MessageTone.Error);
                PasswordBox.Focus();
            }
        }
#pragma warning disable CA1031 // Show any failure on the login card.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            ShowMessage(l => ErrorText.Describe(ex, l), MessageTone.Error);
        }
        finally
        {
            busy = false;
            SubmitButton.IsEnabled = true;
            ApplyLanguage();
        }
    }

    private async void OnSubmitClick(object sender, RoutedEventArgs e) => await SubmitAsync().ConfigureAwait(true);

    private void OnUsernameKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            PasswordBox.Focus();
        }
    }

    private async void OnPasswordKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            await SubmitAsync().ConfigureAwait(true);
        }
    }

    private async void OnServerSettingsClick(object sender, RoutedEventArgs e) => await host.OpenServerSettingsAsync(firstRun: false).ConfigureAwait(true);

    private void OnLanguageChecked(object sender, RoutedEventArgs e)
    {
        var language = ReferenceEquals(sender, ArabicButton) ? AppLanguage.Arabic : AppLanguage.English;
        if (language != UiLanguage.Current)
        {
            host.SetLanguage(language);
        }
    }

    private enum MessageTone
    {
        Info,
        Warning,
        Error,
    }
}
