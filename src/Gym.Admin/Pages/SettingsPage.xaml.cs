using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Admin.Pages;

/// <summary>Placeholder of the settings screen: replace the XAML content and this code with the real screen.</summary>
public partial class SettingsPage : PageView
{
    private object?[] args = [];
    private long money;

    public SettingsPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
    }

    /// <summary>Loads the page's data each time it is shown (here: one number from the API for the placeholder).</summary>
    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        var settings = await Context.Api.GetSettingsAsync(cancellationToken).ConfigureAwait(true);
        money = settings.PrivateTrainingMonthlyFee;
        args = [settings.ExpiringSoonDays];
        ShowDetail();
    }

    public override void OnLanguageChanged() => ShowDetail();

    private void ShowDetail() => Placeholder.Detail = AdminText.SettingsSample.Format(Context.Language, [.. args, Money.Format(money, Context.Language)]);
}
