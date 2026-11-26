using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Admin.Pages;

/// <summary>Placeholder of the staff screen: replace the XAML content and this code with the real screen.</summary>
public partial class StaffPage : PageView
{
    private object?[] args = [];

    public StaffPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
    }

    /// <summary>Loads the page's data each time it is shown (here: one number from the API for the placeholder).</summary>
    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        var staff = await Context.Api.GetStaffAsync(ct: cancellationToken).ConfigureAwait(true);
        args = [staff.Count, staff.Count(s => s.Type == Gym.Domain.StaffType.ClubTrainer)];
        ShowDetail();
    }

    public override void OnLanguageChanged() => ShowDetail();

    private void ShowDetail() => Placeholder.Detail = AdminText.StaffSample.Format(Context.Language, args);
}
