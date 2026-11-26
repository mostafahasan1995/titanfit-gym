using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Admin.Pages;

/// <summary>Placeholder of the reports screen: replace the XAML content and this code with the real screen.</summary>
public partial class ReportsPage : PageView
{
    private long money;

    public ReportsPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
    }

    /// <summary>Loads the page's data each time it is shown (here: one number from the API for the placeholder).</summary>
    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var sales = await Context.Api.GetSalesReportAsync(today.AddDays(-6), today, cancellationToken).ConfigureAwait(true);
        money = sales.Total;
        ShowDetail();
    }

    public override void OnLanguageChanged() => ShowDetail();

    private void ShowDetail() => Placeholder.Detail = AdminText.ReportsSample.Format(Context.Language, Money.Format(money, Context.Language));
}
