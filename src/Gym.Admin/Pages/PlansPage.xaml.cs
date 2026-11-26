using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Admin.Pages;

/// <summary>Placeholder of the plans screen: replace the XAML content and this code with the real screen.</summary>
public partial class PlansPage : PageView
{
    private object?[] args = [];

    public PlansPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
    }

    /// <summary>Loads the page's data each time it is shown (here: one number from the API for the placeholder).</summary>
    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        var plans = await Context.Api.GetPlansAsync(includeInactive: true, ct: cancellationToken).ConfigureAwait(true);
        args = [plans.Count];
        ShowDetail();
    }

    public override void OnLanguageChanged() => ShowDetail();

    private void ShowDetail() => Placeholder.Detail = AdminText.PlansSample.Format(Context.Language, args);
}
