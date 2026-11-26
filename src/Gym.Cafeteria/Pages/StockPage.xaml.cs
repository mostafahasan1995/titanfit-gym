using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Cafeteria.Pages;

/// <summary>Placeholder of the stock screen: replace the XAML content and this code with the real screen.</summary>
public partial class StockPage : PageView
{
    private object?[] args = [];

    public StockPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
    }

    /// <summary>Loads the page's data each time it is shown (here: one number from the API for the placeholder).</summary>
    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        var low = await Context.Api.GetStockAsync(lowOnly: true, ct: cancellationToken).ConfigureAwait(true);
        args = [low.Count];
        ShowDetail();
    }

    public override void OnLanguageChanged() => ShowDetail();

    private void ShowDetail() => Placeholder.Detail = CafeteriaText.StockSample.Format(Context.Language, args);
}
