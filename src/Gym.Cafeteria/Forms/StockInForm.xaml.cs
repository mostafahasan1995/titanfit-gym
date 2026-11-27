using System.Windows.Controls;
using Gym.Cafeteria.Inventory;
using Gym.Cafeteria.Receipts;
using Gym.Contracts;
using Gym.UI;

namespace Gym.Cafeteria.Forms;

/// <summary>The Stock in dialog content.</summary>
public partial class StockInForm : UserControl
{
    private readonly AppLanguage language;

    public StockInForm(ProductDto product, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(product);
        InitializeComponent();
        this.language = language;
        BidiText.SetText(ItemName, ReceiptLayout.ProductName(product.NameEn, product.NameAr, language));
        CurrentText.Text = NumberText.Format(product.Stock);
        Loaded += (_, _) => QuantityInput.Focus();
    }

    public string? Note => NoteInput.Text;

    /// <summary>The quantity, or null after showing the error under the field.</summary>
    public int? ReadQuantity()
    {
        var quantity = StockForms.ParseQuantity(QuantityInput.Text);
        QuantityField.Error = quantity is null ? CafeteriaText.EnterQuantity.Format(language, NumberText.Format(StockForms.MaxQuantity)) : null;
        return quantity;
    }

    public void ShowError(string message) => QuantityField.Error = message;
}
