using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Cafeteria.Pages;

/// <summary>Placeholder of the shift-close screen: replace the XAML content and this code with the real screen.</summary>
public partial class ShiftClosePage : PageView
{
    private int sales = -1;
    private long total;

    public ShiftClosePage(PageContext context)
        : base(context)
    {
        InitializeComponent();
    }

    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        var shift = await Context.Api.GetCurrentShiftAsync(cancellationToken).ConfigureAwait(true);
        if (shift is null)
        {
            sales = -1;
        }
        else
        {
            var summary = await Context.Api.GetShiftSummaryAsync(shift.Id, cancellationToken).ConfigureAwait(true);
            sales = summary.SaleCount;
            total = summary.SalesTotal;
        }

        ShowDetail();
    }

    public override void OnLanguageChanged() => ShowDetail();

    private void ShowDetail() => Placeholder.Detail = sales < 0
        ? CafeteriaText.NoShift.For(Context.Language)
        : CafeteriaText.ShiftSample.Format(Context.Language, sales, Money.Format(total, Context.Language));
}
