using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Reception.Pages;

/// <summary>Placeholder of the private-trainers screen: replace the XAML content and this code with the real screen.</summary>
public partial class PrivateTrainersPage : PageView
{
    private object?[] args = [];

    public PrivateTrainersPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
    }

    /// <summary>Loads the page's data each time it is shown (here: one number from the API for the placeholder).</summary>
    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        var trainers = await Context.Api.GetPrivateTrainersAsync(cancellationToken).ConfigureAwait(true);
        args = [trainers.Count, trainers.Sum(t => t.ClientCount)];
        ShowDetail();
    }

    public override void OnLanguageChanged() => ShowDetail();

    private void ShowDetail() => Placeholder.Detail = ReceptionText.PrivateTrainersSample.Format(Context.Language, args);
}
