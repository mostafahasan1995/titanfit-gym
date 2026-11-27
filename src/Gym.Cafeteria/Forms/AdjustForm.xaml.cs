using System.Windows;
using System.Windows.Controls;
using Gym.Cafeteria.Inventory;
using Gym.Cafeteria.Receipts;
using Gym.Contracts;
using Gym.UI;

namespace Gym.Cafeteria.Forms;

/// <summary>The Adjust stock dialog content. The server refuses a stock below zero; this form says so first.</summary>
public partial class AdjustForm : UserControl
{
    private readonly AppLanguage language;
    private readonly int current;

    public AdjustForm(ProductDto product, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(product);
        InitializeComponent();
        this.language = language;
        current = product.Stock;
        BidiText.SetText(ItemName, ReceiptLayout.ProductName(product.NameEn, product.NameAr, language));
        CurrentText.Text = NumberText.Format(product.Stock);
        UpdateMode();
        Loaded += (_, _) => QuantityInput.Focus();
    }

    public bool IsCount => CountMode.IsChecked == true;

    public string? Reason => ReasonInput.Text;

    /// <summary>The change or the counted quantity, or null after showing the errors.</summary>
    public (int? Delta, int? Counted)? Read()
    {
        var reasonMissing = string.IsNullOrWhiteSpace(ReasonInput.Text);
        ReasonField.Error = reasonMissing ? CafeteriaText.ReasonRequired.For(language) : null;
        var value = IsCount ? StockForms.ParseCount(QuantityInput.Text) : StockForms.ParseChange(QuantityInput.Text);
        if (value is null)
        {
            QuantityField.Error = (IsCount ? CafeteriaText.EnterCount : CafeteriaText.EnterChange).For(language);
            return null;
        }

        var newStock = IsCount ? value.Value : current + value.Value;
        if (newStock < 0)
        {
            ShowNegative();
            return null;
        }

        QuantityField.Error = null;
        if (reasonMissing)
        {
            return null;
        }

        return IsCount ? (Delta: (int?)null, Counted: value) : (Delta: value, Counted: (int?)null);
    }

    /// <summary>The server refused: the stock would go below zero (another till may have sold meanwhile).</summary>
    public void ShowNegative() => QuantityField.Error = CafeteriaText.StockBelowZero.Format(language, NumberText.Format(current));

    public void ShowError(string message) => QuantityField.Error = message;

    /// <summary>Fills in the change and the reason (smoke screenshot).</summary>
    internal void Prefill(string change, string reason)
    {
        ChangeMode.IsChecked = true;
        QuantityInput.Text = change;
        ReasonInput.Text = reason;
    }

    private void OnModeChanged(object sender, RoutedEventArgs e)
    {
        if (IsInitialized)
        {
            UpdateMode();
        }
    }

    private void OnQuantityChanged(object sender, TextChangedEventArgs e) => UpdatePreview();

    private void UpdateMode()
    {
        QuantityField.Label = (IsCount ? CafeteriaText.CountedQuantity : CafeteriaText.ChangeBy).For(language);
        QuantityField.Hint = (IsCount ? CafeteriaText.CountedHint : CafeteriaText.ChangeHint).For(language);
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (NewStockText is null)
        {
            return;
        }

        var value = IsCount ? StockForms.ParseCount(QuantityInput.Text) : StockForms.ParseChange(QuantityInput.Text);
        var newStock = value is null ? current : IsCount ? value.Value : current + value.Value;
        PreviewLabel.Text = CafeteriaText.NewStock.For(language);
        NewStockText.Text = newStock < 0 ? "‎−" + NumberText.Format(-(long)newStock) : NumberText.Format(newStock);
        var negative = newStock < 0;
        PreviewBox.SetResourceReference(Border.BackgroundProperty, negative ? "Brush.BadSoft" : "Brush.AccentSoft");
        PreviewBox.SetResourceReference(Border.BorderBrushProperty, negative ? "Brush.BadLine" : "Brush.AccentLine");
        NewStockText.SetResourceReference(TextBlock.ForegroundProperty, negative ? "Brush.Bad" : "Brush.Text");
        if (negative)
        {
            ShowNegative();
        }
        else if (QuantityField.Error == CafeteriaText.StockBelowZero.Format(language, NumberText.Format(current)))
        {
            QuantityField.Error = null;
        }
    }
}
