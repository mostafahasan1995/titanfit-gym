using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Reception.Pages;

/// <summary>Placeholder of the new-member screen: replace the XAML content and this code with the real screen.</summary>
public partial class NewMemberPage : PageView
{
    private object?[] args = [];

    public NewMemberPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
    }

    /// <summary>Loads the page's data each time it is shown (here: one number from the API for the placeholder).</summary>
    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        var plans = await Context.Api.GetPlansAsync(ct: cancellationToken).ConfigureAwait(true);
        args = [plans.Count];
        ShowDetail();
    }

    public override void OnLanguageChanged() => ShowDetail();

    private void ShowDetail() => Placeholder.Detail = ReceptionText.NewMemberSample.Format(Context.Language, args);
}
