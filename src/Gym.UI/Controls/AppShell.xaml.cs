using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

namespace Gym.UI.Controls;

/// <summary>
/// The shared TITANFIT window layout. Each app passes a <see cref="ShellDefinition"/>;
/// the shell handles the sidebar, the header and the EN / عربي switch (which flips FlowDirection).
/// </summary>
public partial class AppShell : UserControl
{
    private AppLanguage _language;
    private int _selectedPage;

    public AppShell(ShellDefinition definition, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Pages.Count == 0)
        {
            throw new ArgumentException("A shell needs at least one page.", nameof(definition));
        }

        InitializeComponent();
        Definition = definition;
        _language = language;
        Refresh();
    }

    /// <summary>Raised after the user switches the language.</summary>
    public event EventHandler<AppLanguage>? LanguageChanged;

    public ShellDefinition Definition { get; }

    public AppLanguage CurrentLanguage
    {
        get => _language;
        set
        {
            if (value == _language)
            {
                return;
            }

            _language = value;
            Refresh();
            LanguageChanged?.Invoke(this, value);
        }
    }

    private void Refresh()
    {
        var language = _language;
        var page = Definition.Pages[_selectedPage];

        LayoutRoot.FlowDirection = language.ToFlowDirection();
        LayoutRoot.Language = language.ToXmlLanguage();

        AppNameText.Text = Definition.AppName.For(language);
        AutomationProperties.SetName(NavList, Definition.NavLabel.For(language));
        NavList.ItemsSource = Definition.Pages
            .Select((p, i) => new ShellNavItem(i, p.Label.For(language), p.Icon, i == _selectedPage))
            .ToArray();

        StationText.Text = Definition.Station.For(language);
        StaffText.Text = CommonText.NotSignedIn.For(language);
        RoleText.Text = Definition.Role.For(language);
        LogoutText.Text = CommonText.LogOut.For(language);

        TitleText.Text = (page.Title ?? page.Label).For(language);
        SubtitleText.Text = DateText.LongDate(DateTime.Today, language);
        StatusText.Text = CommonText.ServerNotConnected.For(language);
        AutomationProperties.SetName(LanguageGroup, CommonText.Language.For(language));
        EnglishButton.IsChecked = language == AppLanguage.English;
        ArabicButton.IsChecked = language == AppLanguage.Arabic;

        PlaceholderTitle.Text = CommonText.ComingSoonTitle.For(language);
        PlaceholderText.Text = CommonText.ComingSoonText.For(language);
    }

    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: ShellNavItem item } && item.Index != _selectedPage)
        {
            _selectedPage = item.Index;
            Refresh();
        }
    }

    private void OnLanguageChecked(object sender, RoutedEventArgs e) =>
        CurrentLanguage = ReferenceEquals(sender, ArabicButton) ? AppLanguage.Arabic : AppLanguage.English;
}
