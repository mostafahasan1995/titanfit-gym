using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Admin.Pages;

/// <summary>Placeholder of the cafeteria-items screen: replace the XAML content and this code with the real screen.</summary>
public partial class CafeteriaItemsPage : PageView
{
    private object?[] args = [];

    public CafeteriaItemsPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
    }

    /// <summary>Loads the page's data each time it is shown (here: one number from the API for the placeholder).</summary>
    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        var products = await Context.Api.GetProductsAsync(includeInactive: true, ct: cancellationToken).ConfigureAwait(true);
        var categories = await Context.Api.GetCategoriesAsync(includeInactive: true, ct: cancellationToken).ConfigureAwait(true);
        args = [products.Count, categories.Count];
        ShowDetail();
    }

    public override void OnLanguageChanged() => ShowDetail();

    private void ShowDetail() => Placeholder.Detail = AdminText.CafeteriaItemsSample.Format(Context.Language, args);
}
