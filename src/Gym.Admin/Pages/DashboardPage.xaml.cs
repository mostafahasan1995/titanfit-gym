using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Admin.Pages;

/// <summary>Placeholder of the dashboard screen: replace the XAML content and this code with the real screen.</summary>
public partial class DashboardPage : PageView
{
    private object?[] args = [];

    public DashboardPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
    }

    /// <summary>Loads the page's data each time it is shown (here: one number from the API for the placeholder).</summary>
    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        var dashboard = await Context.Api.GetDashboardAsync(cancellationToken).ConfigureAwait(true);
        args = [dashboard.CheckInsToday, dashboard.ActiveMembers];
        ShowDetail();
    }

    public override void OnLanguageChanged() => ShowDetail();

    private void ShowDetail() => Placeholder.Detail = AdminText.DashboardSample.Format(Context.Language, args);
}
