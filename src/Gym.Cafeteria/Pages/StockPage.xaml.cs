using System.Windows;
using System.Windows.Controls;
using Gym.Cafeteria.Forms;
using Gym.Cafeteria.Inventory;
using Gym.Client;
using Gym.UI;
using Gym.UI.Controls;
using Gym.UI.Shell;

namespace Gym.Cafeteria.Pages;

/// <summary>The stock screen. Logic in <see cref="StockViewModel"/>; this class wires the controls and the dialogs.</summary>
public partial class StockPage : PageView
{
    private readonly StockViewModel model;
    private bool loaded;
    private bool syncing;

    public StockPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
        model = new StockViewModel(context.Api, context.Language);
        ItemsGrid.ItemsSource = model.Rows;
        MovementList.ItemsSource = model.Movements;
        model.Rows.CollectionChanged += (_, _) => _ = Dispatcher.InvokeAsync(SyncSelection);
        model.Movements.CollectionChanged += (_, _) => NoHistory.Visibility = model.Selected is not null && model.Movements.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        Context.Live.StockChanged += change => _ = Dispatcher.InvokeAsync(async () =>
        {
            if (!loaded)
            {
                return;
            }

            model.ApplyStock(change.Products);
            UpdateDetail();
            if (model.Selected is { } row && change.Products.Any(p => p.ProductId == row.Id))
            {
                await QuietlyAsync(() => model.SelectAsync(row)).ConfigureAwait(true);
            }
        });
        Context.Live.ProductChanged += change => _ = Dispatcher.InvokeAsync(async () =>
        {
            if (loaded)
            {
                await QuietlyAsync(async () => model.ApplyProduct(await Context.Api.GetProductAsync(change.ProductId).ConfigureAwait(true))).ConfigureAwait(true);
                UpdateDetail();
            }
        });
    }

    public StockViewModel Model => model;

    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        await model.LoadAsync(cancellationToken).ConfigureAwait(true);
        loaded = true;
        FillCategories();
        SyncSelection();
        UpdateDetail();
    }

    public override void OnLanguageChanged()
    {
        model.Language = Context.Language;
        FillCategories();
        UpdateDetail();
    }

    private void FillCategories()
    {
        var selected = model.CategoryId;
        syncing = true;
        CategoryBox.Items.Clear();
        CategoryBox.Items.Add(new ComboBoxItem { Content = CafeteriaText.AllCategories.For(Context.Language), Tag = null });
        foreach (var category in model.Categories.Where(c => c.IsActive))
        {
            CategoryBox.Items.Add(new ComboBoxItem
            {
                Content = Receipts.ReceiptLayout.ProductName(category.NameEn, category.NameAr, Context.Language),
                Tag = category.Id,
            });
        }

        CategoryBox.SelectedItem = CategoryBox.Items.OfType<ComboBoxItem>().FirstOrDefault(i => (int?)i.Tag == selected) ?? CategoryBox.Items[0];
        syncing = false;
    }

    /// <summary>After the list was rebuilt: select the shown item again (without reloading its history).</summary>
    private void SyncSelection()
    {
        syncing = true;
        ItemsGrid.SelectedItem = model.Selected is { } selected ? model.Rows.FirstOrDefault(r => r.Id == selected.Id) : null;
        syncing = false;
        UpdateCounts();
    }

    private void UpdateCounts()
    {
        var language = Context.Language;
        CountText.Text = BidiText.FixNumbers(CafeteriaText.ItemsShown.Format(language, NumberText.Format(model.Rows.Count)), language);
        LowCountPill.Text = model.LowCount == 1
            ? CafeteriaText.OneItemLow.For(language)
            : BidiText.FixNumbers(CafeteriaText.ItemsLow.Format(language, NumberText.Format(model.LowCount)), language);
        LowCountPill.Visibility = model.LowCount > 0 ? Visibility.Visible : Visibility.Collapsed;
        NoItems.Visibility = loaded && model.Rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateDetail()
    {
        UpdateCounts();
        var row = model.Selected;
        DetailPanel.Visibility = row is null ? Visibility.Collapsed : Visibility.Visible;
        NoSelection.Visibility = row is null && loaded ? Visibility.Visible : Visibility.Collapsed;
        if (row is null)
        {
            return;
        }

        BidiText.SetText(DetailName, row.Name);
        BidiText.SetText(DetailCategory, row.CategoryName);
        DetailStatus.Tone = row.StatusTone;
        DetailStatus.Text = row.StatusText;
        DetailStock.Text = row.StockText;
        DetailStock.SetResourceReference(TextBlock.ForegroundProperty, row.IsOut ? "Brush.Bad" : row.IsLow ? "Brush.Warning" : "Brush.Text");
        DetailMin.Text = row.MinStockText;
        DetailPrice.Amount = row.Price;
        EditButton.ToolTip = CafeteriaText.EditItem.For(Context.Language);
        System.Windows.Automation.AutomationProperties.SetName(EditButton, CafeteriaText.EditItem.For(Context.Language));
        NoHistory.Visibility = model.Movements.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async Task QuietlyAsync(Func<Task> work)
    {
        try
        {
            await work().ConfigureAwait(true);
        }
#pragma warning disable CA1031 // A live refresh that fails is caught up by the next visit.
        catch (Exception)
#pragma warning restore CA1031
        {
        }
    }

    private async void OnItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (syncing || ItemsGrid.SelectedItem is not StockRow row || ReferenceEquals(row, model.Selected))
        {
            return;
        }

        try
        {
            await model.SelectAsync(row).ConfigureAwait(true);
            UpdateDetail();
        }
#pragma warning disable CA1031 // Show it; the list stays usable.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Context.Ui.ShowError(ex);
        }
    }

    private void OnSearchChanged(object? sender, EventArgs e) => model.Search = SearchInput.Text;

    private void OnCategoryChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!syncing && CategoryBox.SelectedItem is ComboBoxItem item)
        {
            model.CategoryId = item.Tag as int?;
        }
    }

    private void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        if (model is null)
        {
            return;
        }

        model.LowOnly = LowOnlyBox.IsChecked == true;
        model.IncludeInactive = InactiveBox.IsChecked == true;
    }

    /// <summary>
    /// Smoke screenshot "stock-adjust": the Adjust dialog of an item with no stock and a change of −2 typed in,
    /// so the refusal of a stock below zero shows (the screenshot closes it).
    /// </summary>
    internal async Task OpenAdjustForScreenshotAsync()
    {
        var row = model.Rows.FirstOrDefault(r => r.Stock == 0 && !r.IsInactive) ?? model.Rows.FirstOrDefault();
        if (row is null)
        {
            return;
        }

        await model.SelectAsync(row).ConfigureAwait(true);
        SyncSelection();
        UpdateDetail();
        _ = ShowAdjustAsync(row, form => form.Prefill("-2", CafeteriaText.SampleAdjustReason.For(Context.Language)));
    }

    private async void OnStockInClick(object sender, RoutedEventArgs e)
    {
        if (model.Selected is not { } row)
        {
            return;
        }

        var product = row.Product;
        var form = new StockInForm(product, Context.Language);
        await Context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = CafeteriaText.StockInTitle,
            Content = form,
            PrimaryText = CafeteriaText.SaveStockIn,
            OnPrimary = async () =>
            {
                if (form.ReadQuantity() is not { } quantity)
                {
                    return false;
                }

                try
                {
                    var level = await model.StockInAsync(product.Id, quantity, form.Note).ConfigureAwait(true);
                    Context.Ui.Toast(BidiText.FixNumbers(CafeteriaText.StockInSaved.Format(Context.Language, row.Name, NumberText.Format(level.Stock)), Context.Language));
                    UpdateDetail();
                    return true;
                }
                catch (GymApiException ex)
                {
                    form.ShowError(ErrorText.Describe(ex, Context.Language));
                    return false;
                }
            },
        }).ConfigureAwait(true);
    }

    private async void OnAdjustClick(object sender, RoutedEventArgs e)
    {
        if (model.Selected is { } row)
        {
            await ShowAdjustAsync(row, null).ConfigureAwait(true);
        }
    }

    private async Task ShowAdjustAsync(StockRow row, Action<AdjustForm>? prepare)
    {
        var product = row.Product;
        var form = new AdjustForm(product, Context.Language);
        prepare?.Invoke(form);
        await Context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = CafeteriaText.AdjustTitle,
            Content = form,
            PrimaryText = CafeteriaText.SaveAdjustment,
            OnPrimary = async () =>
            {
                if (form.Read() is not { } values)
                {
                    return false;
                }

                try
                {
                    var level = await model.AdjustAsync(product.Id, values.Delta, values.Counted, form.Reason).ConfigureAwait(true);
                    Context.Ui.Toast(BidiText.FixNumbers(CafeteriaText.AdjustmentSaved.Format(Context.Language, row.Name, NumberText.Format(level.Stock)), Context.Language));
                    UpdateDetail();
                    return true;
                }
                catch (GymApiException ex) when (ex.Code is "negative_stock" or "insufficient_stock")
                {
                    form.ShowNegative();
                    return false;
                }
                catch (GymApiException ex)
                {
                    form.ShowError(ErrorText.Describe(ex, Context.Language));
                    return false;
                }
            },
        }).ConfigureAwait(true);
    }

    private async void OnNewItemClick(object sender, RoutedEventArgs e) => await EditProductAsync(null).ConfigureAwait(true);

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (model.Selected is { } row)
        {
            await EditProductAsync(row.Product).ConfigureAwait(true);
        }
    }

    private async Task EditProductAsync(Gym.Contracts.ProductDto? product)
    {
        var form = new ProductForm(model.Categories, product, Context.Language);
        await Context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = product is null ? CafeteriaText.NewItemTitle : CafeteriaText.EditItem,
            Content = form,
            PrimaryText = CommonText.Save,
            Width = 640,
            OnPrimary = async () =>
            {
                if (form.Read() is not { } request)
                {
                    return false;
                }

                try
                {
                    var saved = await model.SaveProductAsync(product?.Id, request).ConfigureAwait(true);
                    Context.Ui.Toast(CafeteriaText.ItemSaved.Format(Context.Language, Receipts.ReceiptLayout.ProductName(saved.NameEn, saved.NameAr, Context.Language)));
                    SyncSelection();
                    UpdateDetail();
                    return true;
                }
                catch (GymApiException ex)
                {
                    form.ShowServerError(ex);
                    return false;
                }
            },
        }).ConfigureAwait(true);
    }
}
