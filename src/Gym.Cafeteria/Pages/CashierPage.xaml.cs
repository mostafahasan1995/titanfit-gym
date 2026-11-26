using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Cafeteria.Pages;

/// <summary>Placeholder of the cashier screen: replace the XAML content and this code with the real screen.</summary>
public partial class CashierPage : PageView
{
    private object?[] args = [];

    public CashierPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
    }

    /// <summary>Loads the page's data each time it is shown (here: one number from the API for the placeholder).</summary>
    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        var products = await Context.Api.GetProductsAsync(ct: cancellationToken).ConfigureAwait(true);
        var categories = await Context.Api.GetCategoriesAsync(ct: cancellationToken).ConfigureAwait(true);
        args = [products.Count, categories.Count];
        ShowDetail();
    }

    public override void OnLanguageChanged() => ShowDetail();

    private void ShowDetail() => Placeholder.Detail = CafeteriaText.CashierSample.Format(Context.Language, args);
}
