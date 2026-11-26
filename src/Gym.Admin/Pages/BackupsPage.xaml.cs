using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Admin.Pages;

/// <summary>Placeholder of the backups screen: replace the XAML content and this code with the real screen.</summary>
public partial class BackupsPage : PageView
{
    private object?[] args = [];

    public BackupsPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
    }

    /// <summary>Loads the page's data each time it is shown (here: one number from the API for the placeholder).</summary>
    public override Task OnShownAsync(CancellationToken cancellationToken)
    {
        ShowDetail();
        return Task.CompletedTask;
    }

    public override void OnLanguageChanged() => ShowDetail();

    private void ShowDetail() => Placeholder.Detail = AdminText.BackupsSample.Format(Context.Language, args);
}
