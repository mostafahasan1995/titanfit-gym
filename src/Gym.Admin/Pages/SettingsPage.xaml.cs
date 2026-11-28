using System.Globalization;
using System.Windows;
using Gym.Admin.Logic;
using Gym.Client;
using Gym.Contracts;
using Gym.Domain;
using Gym.UI;
using Gym.UI.Controls;
using Gym.UI.Shell;

namespace Gym.Admin.Pages;

public static class SettingsText
{
    public static LocalText RulesTitle { get; } = new("Gym rules", "قواعد النادي");

    public static LocalText RulesText { get; } = new(
        "Used by every PC: reception, the cafeteria and this app. Changes apply at once.",
        "تُستخدم في كل الأجهزة: الاستقبال والكافتيريا وهذا البرنامج. تُطبَّق التغييرات فورًا.");

    public static LocalText ExpiringDays { get; } = new("Expiring-soon warning (days)", "التنبيه بقرب الانتهاء (بالأيام)");

    public static LocalText ExpiringDaysHint { get; } = new(
        "A member shows as expiring this many days before the end date (0 to 60).",
        "يظهر المشترك كمنتهٍ قريبًا قبل تاريخ الانتهاء بهذا العدد من الأيام (من 0 إلى 60).");

    public static LocalText PrivateFee { get; } = new("Private training, monthly fee (US dollars)", "رسم التدريب الخاص الشهري (بالدولار)");

    public static LocalText RoundingStep { get; } = new("Rounding of Syrian pound amounts (pounds)", "تقريب المبالغ بالليرة السورية (بالليرة)");

    public static LocalText RoundingStepHint { get; } = new(
        "Amounts in pounds are rounded to the nearest multiple of this number. 1 = whole pounds; 500 rounds to the nearest 500.",
        "تُقرَّب المبالغ بالليرة إلى أقرب مضاعف لهذا الرقم. 1 = ليرة كاملة؛ 500 تقرّب إلى أقرب 500.");

    public static LocalText PrivateFeeHint { get; } = new(
        "A private trainer pays months × this fee for the training service. The fee is saved with each payment.",
        "يدفع المدرب الخاص عدد الأشهر × هذا الرسم لخدمة التدريب. يُحفظ الرسم مع كل دفعة.");

    public static LocalText Example { get; } = new(
        "With these values: a member whose subscription ends in {0} shows as expiring, and 3 months of private training cost {1}.",
        "بهذه القيم: المشترك الذي ينتهي اشتراكه خلال {0} يظهر كمنتهٍ قريبًا، و3 أشهر من التدريب الخاص تكلّف {1}.");

    /// <summary>{0} = the pounds for 1 dollar.</summary>
    public static LocalText ExampleRate { get; } = new("(pounds at today's rate, $1 = {0})", "(الليرة بسعر اليوم، 1 دولار = {0})");

    public static LocalText Undo { get; } = new("Undo changes", "تراجع عن التغييرات");

    public static LocalText ServerTitle { get; } = new("Server", "الخادم");

    public static LocalText ServerVersion { get; } = new("Server version", "إصدار الخادم");

    public static LocalText ServerAddress { get; } = new("Address", "العنوان");

    public static LocalText ThisPc { get; } = new("This PC", "هذا الجهاز");

    public static LocalText SignedIn { get; } = new("Signed in as", "مسجّل الدخول باسم");

    public static LocalText BackupTitle { get; } = new("Daily backup", "النسخ الاحتياطي اليومي");

    public static LocalText BackupWhen { get; } = new("Runs", "الموعد");

    public static LocalText BackupDaily { get; } = new("Every day at {0} · keeps the last {1}", "كل يوم الساعة {0} · يحتفظ بآخر {1} نسخة");

    public static LocalText BackupFolder { get; } = new("Folder", "المجلد");

    public static LocalText BackupLast { get; } = new("Last backup", "آخر نسخة");

    public static LocalText BackupNone { get; } = new("None yet", "لا يوجد بعد");

    public static LocalText BackupHint { get; } = new("Change the folder, time and how many to keep on the Backups page.", "يمكن تغيير المجلد والموعد وعدد النسخ من صفحة النسخ الاحتياطي.");

    public static LocalText BackupOk { get; } = new("Up to date", "محدّث");

    public static LocalText BackupAttention { get; } = new("Needs attention", "يحتاج إلى متابعة");
}

/// <summary>Settings: expiring-soon threshold and private training fee; server, this PC and the backup summary.</summary>
public partial class SettingsPage : PageView
{
    private SettingsDto? settings;
    private BackupStatusDto? backup;
    private string? version;

    public SettingsPage(PageContext context)
        : base(context)
    {
        InitializeComponent();
        DaysBox.TextChanged += (_, _) => ShowExample();
        FeeBox.TextChanged += (_, _) => ShowExample();
        StepBox.TextChanged += (_, _) => ShowExample();
    }

    public override async Task OnShownAsync(CancellationToken cancellationToken)
    {
        settings = await Context.Api.GetSettingsAsync(cancellationToken).ConfigureAwait(true);
        version = (await Context.Api.GetHealthAsync(cancellationToken).ConfigureAwait(true)).Version;
        try
        {
            backup = await Context.Api.GetBackupStatusAsync(cancellationToken).ConfigureAwait(true);
        }
        catch (GymApiException)
        {
            backup = null;
        }

        FillForm();
        ShowInfo();
    }

    public override void OnLanguageChanged()
    {
        ShowInfo();
        ShowExample();
        if (DaysField.Error is not null || FeeField.Error is not null || StepField.Error is not null)
        {
            Validate();
        }
    }

    private void FillForm()
    {
        if (settings is not { } s)
        {
            return;
        }

        DaysBox.Text = s.ExpiringSoonDays.ToString(CultureInfo.InvariantCulture);
        FeeBox.Text = Money.FormatAmount(s.PrivateTrainingMonthlyFee);
        StepBox.Text = s.SypRoundingStep.ToString(CultureInfo.InvariantCulture);
        DaysField.Error = null;
        FeeField.Error = null;
        StepField.Error = null;
        ShowExample();
    }

    private void ShowInfo()
    {
        var language = Context.Language;
        VersionText.Text = version ?? "—";
        AddressText.Text = Context.Api.BaseAddress?.GetLeftPart(UriPartial.Authority) ?? "—";
        BidiText.SetText(StationText, Context.StationName);
        BidiText.SetText(UserText, Context.Session.Staff.FullName);
        if (backup is { } b)
        {
            BackupWhen.Text = SettingsText.BackupDaily.Format(language, b.Time.ToString("HH:mm", CultureInfo.InvariantCulture), NumberText.Format(b.KeepLast));
            BackupFolder.Text = b.Folder;
            BackupLast.Text = b.LastSuccess is { } last ? DateText.DateAndTime(last.FinishedAt, language) : SettingsText.BackupNone.For(language);
            var ok = b.Warning == BackupWarning.None;
            BackupPill.Tone = ok ? PillTone.Good : b.Warning == BackupWarning.LastFailed ? PillTone.Bad : PillTone.Warning;
            BackupPill.Text = (ok ? SettingsText.BackupOk : SettingsText.BackupAttention).For(language);
            BackupPill.Visibility = Visibility.Visible;
        }
        else
        {
            BackupWhen.Text = BackupFolder.Text = BackupLast.Text = "—";
            BackupPill.Visibility = Visibility.Collapsed;
        }
    }

    private void ShowExample()
    {
        var language = Context.Language;
        var result = SettingsForm.Validate(DaysBox.Text, FeeBox.Text, StepBox.Text);
        if (!result.IsValid)
        {
            ExampleText.Text = "";
            return;
        }

        // Three months in dollars, and in pounds at today's rate with the typed rounding step (when a rate exists).
        var usd = result.PrivateTrainingFee!.Value * 3;
        var rate = ExchangeRates.Current.SypPerUsd;
        var syp = rate is { } r && r > 0 ? CurrencyRules.ToSyp(usd, r, result.SypRoundingStep!.Value) : (decimal?)null;
        var text = SettingsText.Example.Format(language, AdminText.Days(result.ExpiringSoonDays!.Value, language), Money.FormatWithSyp(usd, syp, language));
        if (rate is { } shown && syp is not null)
        {
            text += " " + SettingsText.ExampleRate.Format(language, Money.FormatSyp(shown, language));
        }

        ExampleText.Text = BidiText.FixNumbers(text, language);
    }

    private SettingsForm.Result Validate()
    {
        var language = Context.Language;
        var result = SettingsForm.Validate(DaysBox.Text, FeeBox.Text, StepBox.Text);
        DaysField.Error = result.DaysError?.For(language);
        FeeField.Error = result.FeeError?.For(language);
        StepField.Error = result.StepError?.For(language);
        return result;
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        var result = Validate();
        if (!result.IsValid)
        {
            return;
        }

        SaveButton.IsEnabled = false;
        try
        {
            settings = await Context.Api.UpdateSettingsAsync(
                new SettingsDto(result.ExpiringSoonDays!.Value, result.PrivateTrainingFee!.Value, result.SypRoundingStep!.Value)).ConfigureAwait(true);
            FillForm();
            Context.Ui.Toast(CommonText.SettingsSaved);
        }
#pragma warning disable CA1031 // Show any failure as a toast; the form keeps what was typed.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            Context.Ui.Toast(AdminErrors.Describe(ex, Context.Language), ToastKind.Error);
        }
        finally
        {
            SaveButton.IsEnabled = true;
        }
    }

    private void OnUndoClick(object sender, RoutedEventArgs e) => FillForm();
}
