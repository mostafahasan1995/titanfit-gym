using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Gym.Admin.Controls;
using Gym.Admin.Logic;
using Gym.Client;
using Gym.Contracts;
using Gym.Domain;
using Gym.UI;
using Gym.UI.Controls;
using Gym.UI.Shell;

namespace Gym.Admin.Pages;

public static class ItemsText
{
    public static LocalText ShowInactive { get; } = new("Show inactive", "إظهار غير الفعّالة");

    public static LocalText AddItem { get; } = new("Add item", "إضافة صنف");

    public static LocalText EditItem { get; } = new("Edit item", "تعديل الصنف");

    public static LocalText AllCategories { get; } = new("All categories", "كل الفئات");

    public static LocalText Item { get; } = new("Item", "الصنف");

    public static LocalText Category { get; } = new("Category", "الفئة");

    public static LocalText Barcode { get; } = new("Barcode", "الباركود");

    public static LocalText Price { get; } = new("Price", "السعر");

    public static LocalText Stock { get; } = new("In stock", "في المخزون");

    public static LocalText Minimum { get; } = new("Min", "الحد الأدنى");

    public static LocalText Status { get; } = new("Status", "الحالة");

    public static LocalText StockButton { get; } = new("Stock", "المخزون");

    public static LocalText ItemCount { get; } = new("{0} items", "عدد الأصناف {0}");

    public static LocalText LowCount { get; } = new("{0} low", "منخفض: {0}");

    public static LocalText EmptyCount { get; } = new("{0} out of stock", "نفد: {0}");

    public static LocalText Low { get; } = new("Low", "منخفض");

    public static LocalText Out { get; } = new("Out", "نفد");

    public static LocalText NoItemsText { get; } = new("Try another name or barcode, or add an item.", "جرّب اسمًا أو باركودًا آخر، أو أضف صنفًا.");

    public static LocalText NameEn { get; } = new("Name in English", "الاسم بالإنجليزية");

    public static LocalText NameAr { get; } = new("Name in Arabic", "الاسم بالعربية");

    public static LocalText PriceLabel { get; } = new("Price (US dollars)", "السعر (بالدولار)");

    public static LocalText MinimumLabel { get; } = new("Low-stock minimum", "الحد الأدنى للمخزون");

    public static LocalText MinimumHint { get; } = new("Below this the item shows as low stock.", "تحت هذا العدد يظهر الصنف كمخزون منخفض.");

    public static LocalText BarcodeHint { get; } = new("Scan it or type it. Leave empty if none.", "امسحه أو اكتبه. اتركه فارغًا إن لم يوجد.");

    public static LocalText ActiveLabel { get; } = new("Active (for sale in the cafeteria)", "فعّال (معروض للبيع في الكافتيريا)");

    public static LocalText StockTitle { get; } = new("Stock · {0}", "المخزون · {0}");

    public static LocalText StockNow { get; } = new("In stock now: {0} · minimum {1}", "في المخزون الآن: {0} · الحد الأدنى {1}");

    public static LocalText StockIn { get; } = new("Add a delivery", "إضافة كمية واردة");

    public static LocalText StockCount { get; } = new("Set the counted quantity (stocktake)", "تثبيت الكمية بعد الجرد");

    public static LocalText Change { get; } = new("What to do", "الإجراء");

    public static LocalText Quantity { get; } = new("Quantity", "الكمية");

    public static LocalText StockNote { get; } = new(
        "Every change is saved as a stock movement with your name. The stock never goes below zero.",
        "كل تغيير يُحفظ كحركة مخزون باسمك. لا يمكن أن يصبح المخزون أقل من صفر.");

    public static LocalText StockSaved { get; } = new("Stock saved: {0} in stock.", "تم حفظ المخزون: الكمية الآن {0}.");
}

/// <summary>Cafeteria items: list with stock, add and edit items, deliveries and stocktakes.</summary>
public partial class CafeteriaItemsPage : PageView
{
    private IReadOnlyList<ProductDto> products = [];
    private IReadOnlyList<CategoryDto> categories = [];
    private bool ready;

    public CafeteriaItemsPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
        ready = true;
    }

    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        categories = await Context.Api.GetCategoriesAsync(includeInactive: true, ct: cancellationToken).ConfigureAwait(true);
        FillCategories();
        await LoadAsync(cancellationToken).ConfigureAwait(true);
    }

    public override void OnLanguageChanged()
    {
        FillCategories();
        ShowItems();
    }

    private void FillCategories()
    {
        var language = Context.Language;
        var selected = Forms.Selected<int?>(CategoryBox);
        var choices = new List<Choice> { new(null, ItemsText.AllCategories.For(language)) };
        choices.AddRange(categories.OrderBy(c => c.SortOrder).Select(c => new Choice(c.Id, CategoryName(c, language))));
        ready = false;
        CategoryBox.ItemsSource = choices;
        CategoryBox.SelectedItem = choices.FirstOrDefault(c => Equals(c.Value, selected)) ?? choices[0];
        ready = true;
    }

    private async Task LoadAsync(CancellationToken ct)
    {
        var query = SearchBox.Text.Trim();
        products = await Context.Api.GetProductsAsync(
            query.Length == 0 ? null : query, Forms.Selected<int?>(CategoryBox), InactiveBox.IsChecked == true, ct).ConfigureAwait(true);
        ShowItems();
    }

    private void ShowItems()
    {
        var language = Context.Language;
        var rows = products
            .OrderBy(p => categories.FirstOrDefault(c => c.Id == p.CategoryId)?.SortOrder ?? 99)
            .ThenBy(p => language == AppLanguage.Arabic ? p.NameAr : p.NameEn, StringComparer.CurrentCulture)
            .Select(p => ToRow(p, language))
            .ToList();
        ItemsGrid.ItemsSource = rows;
        ListEmpty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountText.Text = ItemsText.ItemCount.Format(language, NumberText.Format(rows.Count));
        var low = products.Count(p => p.IsActive && p.IsLowStock && p.Stock > 0);
        var empty = products.Count(p => p.IsActive && p.Stock == 0);
        LowPill.Text = ItemsText.LowCount.Format(language, NumberText.Format(low));
        LowPill.Visibility = low > 0 ? Visibility.Visible : Visibility.Collapsed;
        EmptyPill.Text = ItemsText.EmptyCount.Format(language, NumberText.Format(empty));
        EmptyPill.Visibility = empty > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private ItemRow ToRow(ProductDto p, AppLanguage language)
    {
        var name = language == AppLanguage.Arabic ? p.NameAr : p.NameEn;
        var other = language == AppLanguage.Arabic ? p.NameEn : p.NameAr;
        var (tone, text) = p.Stock == 0 ? (PillTone.Bad, ItemsText.Out.For(language))
            : p.IsLowStock ? (PillTone.Warning, ItemsText.Low.For(language))
            : (PillTone.Neutral, "");
        return new ItemRow(
            p,
            name,
            Direction(name),
            other,
            Direction(other),
            language == AppLanguage.Arabic ? p.CategoryNameAr : p.CategoryNameEn,
            p.Barcode ?? AdminText.Dash.For(language),
            p.Price,
            NumberText.Format(p.Stock),
            p.Stock == 0 ? ThemeBrush("Brush.Bad") : p.IsLowStock ? ThemeBrush("Brush.Warning") : ThemeBrush("Brush.Text"),
            tone,
            text,
            text.Length == 0 ? Visibility.Collapsed : Visibility.Visible,
            NumberText.Format(p.MinStock),
            (p.IsActive ? AdminText.Active : AdminText.Inactive).For(language),
            p.IsActive ? PillTone.Good : PillTone.Neutral);
    }

    private async Task EditAsync(ProductDto? product)
    {
        var language = Context.Language;
        var nameEn = Forms.Text(ItemsText.NameEn, product?.NameEn, leftToRight: true, maxLength: Product.NameMaxLength);
        var nameAr = Forms.Text(ItemsText.NameAr, product?.NameAr, maxLength: Product.NameMaxLength);
        nameAr.Box.FlowDirection = FlowDirection.RightToLeft;
        var category = Forms.Pick(
            ItemsText.Category,
            categories.Where(c => c.IsActive || c.Id == product?.CategoryId).OrderBy(c => c.SortOrder).Select(c => new Choice(c.Id, CategoryName(c, language))),
            product?.CategoryId);
        var price = Forms.Text(ItemsText.PriceLabel, product is null ? null : Money.FormatAmount(product.Price), leftToRight: true, maxLength: 15);
        var barcode = Forms.Text(ItemsText.Barcode, product?.Barcode, leftToRight: true, hint: ItemsText.BarcodeHint, maxLength: Product.BarcodeMaxLength);
        var minimum = Forms.Text(ItemsText.MinimumLabel, (product?.MinStock ?? 5).ToString(CultureInfo.InvariantCulture), leftToRight: true, hint: ItemsText.MinimumHint, maxLength: 6);
        var active = Forms.Check(ItemsText.ActiveLabel, product?.IsActive ?? true);
        var content = Forms.Stack(
            Forms.Columns(nameEn.Field, nameAr.Field),
            Forms.Columns(category.Field, price.Field),
            Forms.Columns(barcode.Field, minimum.Field),
            active);

        await Context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = product is null ? ItemsText.AddItem : ItemsText.EditItem,
            Content = content,
            PrimaryText = CommonText.Save,
            Width = 640,
            OnPrimary = async () =>
            {
                var (en, enError) = FormChecks.Required(nameEn.Box.Text);
                var (ar, arError) = FormChecks.Required(nameAr.Box.Text);
                var (priceValue, priceError) = FormChecks.Amount(price.Box.Text);
                var (minValue, minError) = FormChecks.WholeNumber(minimum.Box.Text, 0, 100_000);
                nameEn.Field.Error = enError?.For(language);
                nameAr.Field.Error = arError?.For(language);
                price.Field.Error = priceError?.For(language);
                minimum.Field.Error = minError?.For(language);
                var categoryId = Forms.Selected<int>(category.Box);
                if (en is null || ar is null || priceValue is not { } p || minValue is not { } min || categoryId <= 0)
                {
                    return false;
                }

                var request = new SaveProductRequest(en, ar, categoryId, p, FormChecks.Optional(barcode.Box.Text), min, active.IsChecked == true);
                try
                {
                    _ = product is null
                        ? await Context.Api.CreateProductAsync(request).ConfigureAwait(true)
                        : await Context.Api.UpdateProductAsync(product.Id, request).ConfigureAwait(true);
                }
                catch (GymApiException ex)
                {
                    if (ex.Code == "duplicate_barcode")
                    {
                        barcode.Field.Error = AdminErrors.Describe(ex, language);
                    }
                    else
                    {
                        Context.Ui.Toast(AdminErrors.Describe(ex, language), ToastKind.Error);
                    }

                    return false;
                }

                Context.Ui.Toast(AdminText.Saved);
                await LoadAsync(CancellationToken.None).ConfigureAwait(true);
                return true;
            },
        }).ConfigureAwait(true);
    }

    private async Task StockAsync(ProductDto product)
    {
        var language = Context.Language;
        var now = Forms.Note(ItemsText.StockNow.Format(language, NumberText.Format(product.Stock), NumberText.Format(product.MinStock)));
        now.SetResourceReference(TextBlock.StyleProperty, "Text.Strong");
        var mode = Forms.Pick(ItemsText.Change, [new Choice(true, ItemsText.StockIn.For(language)), new Choice(false, ItemsText.StockCount.For(language))], true);
        var quantity = Forms.Text(ItemsText.Quantity, null, leftToRight: true, maxLength: 6);
        var note = Forms.Text(AdminText.Note, null, maxLength: StockMovement.NoteMaxLength);
        var content = Forms.Stack(now, Forms.Columns(mode.Field, quantity.Field), note.Field, Forms.Note(ItemsText.StockNote.For(language)));

        await Context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = new LocalText(ItemsText.StockTitle.Format(AppLanguage.English, product.NameEn), ItemsText.StockTitle.Format(AppLanguage.Arabic, product.NameAr)),
            Content = content,
            PrimaryText = CommonText.Save,
            Width = 600,
            OnPrimary = async () =>
            {
                var adding = Forms.Selected<bool>(mode.Box);
                var (value, error) = FormChecks.WholeNumber(quantity.Box.Text, adding ? 1 : 0, 100_000);
                quantity.Field.Error = error?.For(language);
                if (value is not { } q)
                {
                    return false;
                }

                StockLevelDto level;
                try
                {
                    level = adding
                        ? await Context.Api.StockInAsync(new StockInRequest(product.Id, q, FormChecks.Optional(note.Box.Text))).ConfigureAwait(true)
                        : await Context.Api.AdjustStockAsync(new StockAdjustmentRequest(product.Id, CountedQuantity: q, Note: FormChecks.Optional(note.Box.Text))).ConfigureAwait(true);
                }
                catch (GymApiException ex)
                {
                    quantity.Field.Error = AdminErrors.Describe(ex, language);
                    return false;
                }

                Context.Ui.Toast(ItemsText.StockSaved.Format(language, NumberText.Format(level.Stock)));
                await LoadAsync(CancellationToken.None).ConfigureAwait(true);
                return true;
            },
        }).ConfigureAwait(true);
    }

    /// <summary>Smoke screenshots: the stock dialog of the first low item (not awaited: the dialog stays open).</summary>
    internal void StockForSmoke()
    {
        if ((products.OrderBy(p => p.Stock).FirstOrDefault(p => p.IsLowStock) ?? products.FirstOrDefault()) is { } product)
        {
            _ = StockAsync(product);
        }
    }

    private static string CategoryName(CategoryDto c, AppLanguage language) => language == AppLanguage.Arabic ? c.NameAr : c.NameEn;

    private static FlowDirection Direction(string? text) => BidiText.HasRtl(text) ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

    private static Brush ThemeBrush(string key) => Application.Current?.TryFindResource(key) as Brush ?? Brushes.White;

    private async Task RunAsync(Func<Task> work)
    {
        try
        {
            await work().ConfigureAwait(true);
        }
#pragma warning disable CA1031 // Show any failure as a toast; the page stays usable.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Context.Ui.ShowError(ex);
        }
    }

    private async void OnSearchChanged(object? sender, EventArgs e) => await RunAsync(() => LoadAsync(CancellationToken.None)).ConfigureAwait(true);

    private async void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        if (ready && IsLoaded)
        {
            await RunAsync(() => LoadAsync(CancellationToken.None)).ConfigureAwait(true);
        }
    }

    private async void OnAddClick(object sender, RoutedEventArgs e) => await RunAsync(() => EditAsync(null)).ConfigureAwait(true);

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ItemRow row })
        {
            await RunAsync(() => EditAsync(row.Product)).ConfigureAwait(true);
        }
    }

    private async void OnStockClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ItemRow row })
        {
            await RunAsync(() => StockAsync(row.Product)).ConfigureAwait(true);
        }
    }

    private async void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (ItemsGrid.SelectedItem is ItemRow row && e.OriginalSource is not System.Windows.Controls.Primitives.ButtonBase)
        {
            await RunAsync(() => EditAsync(row.Product)).ConfigureAwait(true);
        }
    }

    /// <summary>A row of the items list.</summary>
    public sealed record ItemRow(
        ProductDto Product,
        string Name,
        FlowDirection NameDirection,
        string OtherName,
        FlowDirection OtherDirection,
        string Category,
        string Barcode,
        decimal Price,
        string Stock,
        Brush StockBrush,
        PillTone StockTone,
        string StockText,
        Visibility StockPillVisibility,
        string Minimum,
        string StatusText,
        PillTone Tone);
}
