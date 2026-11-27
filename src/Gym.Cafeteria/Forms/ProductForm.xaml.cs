using System.Windows;
using System.Windows.Controls;
using Gym.Cafeteria.Inventory;
using Gym.Cafeteria.Receipts;
using Gym.Client;
using Gym.Contracts;
using Gym.UI;

namespace Gym.Cafeteria.Forms;

/// <summary>The New item / Edit item dialog content.</summary>
public partial class ProductForm : UserControl
{
    private readonly AppLanguage language;

    /// <param name="product">The item to edit, or null for a new one.</param>
    public ProductForm(IReadOnlyList<CategoryDto> categories, ProductDto? product, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(categories);
        InitializeComponent();
        this.language = language;
        foreach (var category in categories.Where(c => c.IsActive || c.Id == product?.CategoryId))
        {
            CategoryInput.Items.Add(new ComboBoxItem { Content = ReceiptLayout.ProductName(category.NameEn, category.NameAr, language), Tag = category.Id });
        }

        if (product is not null)
        {
            NameEnInput.Text = product.NameEn;
            NameArInput.Text = product.NameAr;
            PriceInput.Text = NumberText.Format(product.Price);
            BarcodeInput.Text = product.Barcode ?? "";
            MinInput.Text = NumberText.Format(product.MinStock);
            ActiveInput.IsChecked = product.IsActive;
        }
        else
        {
            MinInput.Text = "0";
        }

        CategoryInput.SelectedItem = CategoryInput.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (int)i.Tag == product?.CategoryId)
                                     ?? CategoryInput.Items.OfType<ComboBoxItem>().FirstOrDefault();
        Loaded += (_, _) => (language == AppLanguage.Arabic ? NameArInput : NameEnInput).Focus();
    }

    /// <summary>The request, or null after showing the errors under the fields.</summary>
    public SaveProductRequest? Read()
    {
        ClearErrors();
        var nameEn = NameEnInput.Text.Trim();
        var nameAr = NameArInput.Text.Trim();
        var ok = true;
        if (nameEn.Length == 0 && nameAr.Length == 0)
        {
            NameEnField.Error = CafeteriaText.NameRequired.For(language);
            ok = false;
        }

        if (CategoryInput.SelectedItem is not ComboBoxItem { Tag: int categoryId })
        {
            CategoryField.Error = CafeteriaText.CategoryRequired.For(language);
            ok = false;
            categoryId = 0;
        }

        var price = Money.Parse(PriceInput.Text);
        if (price is null or < 0)
        {
            PriceField.Error = CafeteriaText.EnterPrice.For(language);
            ok = false;
        }

        var min = StockForms.ParseCount(MinInput.Text);
        if (min is null)
        {
            MinField.Error = CafeteriaText.EnterCount.For(language);
            ok = false;
        }

        if (!ok)
        {
            return null;
        }

        var barcode = BarcodeInput.Text.Trim();
        return new SaveProductRequest(
            nameEn.Length > 0 ? nameEn : nameAr,
            nameAr.Length > 0 ? nameAr : nameEn,
            categoryId,
            price!.Value,
            barcode.Length == 0 ? null : barcode,
            min!.Value,
            ActiveInput.IsChecked == true);
    }

    /// <summary>Shows a refusal from the server next to the right field.</summary>
    public void ShowServerError(Exception error)
    {
        ArgumentNullException.ThrowIfNull(error);
        if (error is GymApiException { Code: "duplicate_barcode" })
        {
            BarcodeField.Error = ErrorText.DuplicateBarcode.For(language);
            return;
        }

        FormError.Text = ErrorText.Describe(error, language);
        FormError.Visibility = Visibility.Visible;
    }

    private void ClearErrors()
    {
        NameEnField.Error = null;
        CategoryField.Error = null;
        PriceField.Error = null;
        MinField.Error = null;
        BarcodeField.Error = null;
        FormError.Visibility = Visibility.Collapsed;
    }
}
