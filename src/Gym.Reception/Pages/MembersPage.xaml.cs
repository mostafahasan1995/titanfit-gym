using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Reception.Pages;

/// <summary>Placeholder of the members screen: replace the XAML content and this code with the real screen.</summary>
public partial class MembersPage : PageView
{
    private object?[] args = [];

    public MembersPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
    }

    /// <summary>Loads the page's data each time it is shown (here: one number from the API for the placeholder).</summary>
    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        var members = await Context.Api.SearchMembersAsync(pageSize: 1, ct: cancellationToken).ConfigureAwait(true);
        args = [members.Total];
        ShowDetail();
    }

    public override void OnLanguageChanged() => ShowDetail();

    private void ShowDetail() => Placeholder.Detail = ReceptionText.MembersSample.Format(Context.Language, args);
}
