using System.Globalization;
using System.Windows;
using System.Windows.Input;
using Gym.Admin.Controls;
using Gym.Admin.Logic;
using Gym.Client;
using Gym.Contracts;
using Gym.UI;
using Gym.UI.Controls;
using Gym.UI.Shell;

namespace Gym.Admin.Pages;

public static class PlansText
{
    public static LocalText ListTitle { get; } = new("Membership plans", "خطط الاشتراك");

    public static LocalText PlanCount { get; } = new("{0} active · {1} inactive", "{0} فعّالة · {1} غير فعّالة");

    public static LocalText AddPlan { get; } = new("Add plan", "إضافة خطة");

    public static LocalText EditPlan { get; } = new("Edit plan", "تعديل الخطة");

    public static LocalText Plan { get; } = new("Plan", "الخطة");

    public static LocalText Price { get; } = new("Price", "السعر");

    public static LocalText PerMonth { get; } = new("Per month", "شهريًا");

    public static LocalText Status { get; } = new("Status", "الحالة");

    public static LocalText MonthsLabel { get; } = new("Months", "عدد الأشهر");

    public static LocalText PriceLabel { get; } = new("Price (US dollars)", "السعر (بالدولار)");

    public static LocalText ActiveLabel { get; } = new("Active (reception can sell it)", "فعّالة (يمكن للاستقبال بيعها)");

    public static LocalText SaveNote { get; } = new(
        "A new price applies to new payments only. Subscriptions already paid keep their price.",
        "السعر الجديد يُطبَّق على الدفعات الجديدة فقط. الاشتراكات المدفوعة تحتفظ بسعرها.");

    public static LocalText HowTitle { get; } = new("How plans work", "كيف تعمل الخطط");

    public static LocalText HowPrice { get; } = new(
        "The price is copied into each subscription when it is paid, so later changes never touch old payments.",
        "يُنسخ السعر إلى كل اشتراك عند دفعه، لذلك لا تؤثر التعديلات اللاحقة على الدفعات القديمة.");

    public static LocalText HowInactive { get; } = new(
        "An inactive plan is hidden at reception but stays in the history.",
        "الخطة غير الفعّالة تختفي من الاستقبال لكنها تبقى في السجل.");

    public static LocalText HowRenewal { get; } = new(
        "An early renewal starts the day after the current end date, so no paid day is lost.",
        "التجديد المبكر يبدأ في اليوم التالي لتاريخ الانتهاء الحالي، فلا يضيع أي يوم مدفوع.");

    public static LocalText PrivateFee { get; } = new("Private training · monthly fee", "التدريب الخاص · الرسم الشهري");

    public static LocalText PrivateFeeHint { get; } = new("Change it on the Settings page.", "يمكن تغييره من صفحة الإعدادات.");
}

/// <summary>Plans and prices: list, add, edit (months, price, active).</summary>
public partial class PlansPage : PageView
{
    private IReadOnlyList<PlanDto> plans = [];

    public PlansPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
    }

    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        plans = await Context.Api.GetPlansAsync(includeInactive: true, ct: cancellationToken).ConfigureAwait(true);
        var settings = await Context.Api.GetSettingsAsync(cancellationToken).ConfigureAwait(true);
        FeeText.Amount = settings.PrivateTrainingMonthlyFee;
        FeeSypText.Amount = settings.PrivateTrainingMonthlyFee;
        ShowPlans();
    }

    public override void OnLanguageChanged() => ShowPlans();

    private void ShowPlans()
    {
        var language = Context.Language;
        CountText.Text = PlansText.PlanCount.Format(language, plans.Count(p => p.IsActive), plans.Count(p => !p.IsActive));
        PlansGrid.ItemsSource = plans
            .OrderByDescending(p => p.IsActive).ThenBy(p => p.Months).ThenBy(p => p.Price)
            .Select(p => new PlanRow(
                p,
                AdminText.Months(p.Months, language),
                p.Price,
                p.Months > 0 ? Gym.Domain.CurrencyRules.RoundUsd(p.Price / p.Months) : p.Price,
                (p.IsActive ? AdminText.Active : AdminText.Inactive).For(language),
                p.IsActive ? PillTone.Good : PillTone.Neutral))
            .ToList();
    }

    private async Task EditAsync(PlanDto? plan)
    {
        var language = Context.Language;
        var months = Forms.Text(PlansText.MonthsLabel, plan?.Months.ToString(CultureInfo.InvariantCulture), leftToRight: true, maxLength: 3);
        var price = Forms.Text(PlansText.PriceLabel, plan is null ? null : Money.FormatAmount(plan.Price), leftToRight: true, maxLength: 15);
        var active = Forms.Check(PlansText.ActiveLabel, plan?.IsActive ?? true);
        var content = Forms.Stack(Forms.Columns(months.Field, price.Field), active, Forms.Note(PlansText.SaveNote.For(language)));

        await Context.Ui.ShowDialogAsync(new DialogRequest
        {
            Title = plan is null ? PlansText.AddPlan : PlansText.EditPlan,
            Content = content,
            PrimaryText = CommonText.Save,
            OnPrimary = async () =>
            {
                var (monthValue, monthError) = FormChecks.WholeNumber(months.Box.Text, 1, 120);
                var (priceValue, priceError) = FormChecks.Amount(price.Box.Text);
                months.Field.Error = monthError?.For(language);
                price.Field.Error = priceError?.For(language);
                if (monthValue is not { } m || priceValue is not { } p)
                {
                    return false;
                }

                var request = new SavePlanRequest(m, p, active.IsChecked == true);
                try
                {
                    _ = plan is null
                        ? await Context.Api.CreatePlanAsync(request).ConfigureAwait(true)
                        : await Context.Api.UpdatePlanAsync(plan.Id, request).ConfigureAwait(true);
                }
                catch (GymApiException ex)
                {
                    Context.Ui.Toast(AdminErrors.Describe(ex, language), ToastKind.Error);
                    return false;
                }

                Context.Ui.Toast(AdminText.Saved);
                await OnShownAsync(CancellationToken.None).ConfigureAwait(true);
                return true;
            },
        }).ConfigureAwait(true);
    }

    private async void OnAddClick(object sender, RoutedEventArgs e) => await EditAsync(null).ConfigureAwait(true);

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: PlanRow row })
        {
            await EditAsync(row.Plan).ConfigureAwait(true);
        }
    }

    private async void OnGridDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (PlansGrid.SelectedItem is PlanRow row)
        {
            await EditAsync(row.Plan).ConfigureAwait(true);
        }
    }

    /// <summary>A row of the plans list.</summary>
    public sealed record PlanRow(PlanDto Plan, string Name, decimal Price, decimal PerMonth, string StatusText, PillTone Tone);
}
