using Gym.Admin;
using Gym.Admin.Logic;
using Gym.Client;
using Gym.UI;

namespace Gym.Tests.Admin;

/// <summary>The Admin app's input checks and display helpers.</summary>
public sealed class AdminFormTests
{
    [Theory]
    [InlineData("3", "25", "1", 3, "25", 1)]
    [InlineData(" 0 ", "12.50", "500", 0, "12.5", 500)]
    [InlineData("60", "0", "1,000", 60, "0", 1000)]
    [InlineData("٧", "١٢٫٥", "١", 7, "12.5", 1)] // Arabic-Indic digits and decimal sign typed on an Arabic keyboard
    [InlineData("3", "$1,250.75", "1", 3, "1250.75", 1)]
    public void Settings_form_accepts_valid_values(string days, string fee, string step, int expectedDays, string expectedFee, int expectedStep)
    {
        var result = SettingsForm.Validate(days, fee, step);

        Assert.True(result.IsValid);
        Assert.Equal(
            (expectedDays, decimal.Parse(expectedFee, System.Globalization.CultureInfo.InvariantCulture), expectedStep),
            (result.ExpiringSoonDays!.Value, result.PrivateTrainingFee!.Value, result.SypRoundingStep!.Value));
    }

    [Fact]
    public void Settings_form_checks_the_rounding_step()
    {
        Assert.Equal(1, SettingsForm.Validate("3", "25").SypRoundingStep);

        var zero = SettingsForm.Validate("3", "25", "0");
        Assert.False(zero.IsValid);
        Assert.Equal("Enter a number from 1 to 100000.", zero.StepError?.En);

        Assert.Equal(AdminText.WholeNumber, SettingsForm.Validate("3", "25", "2.5").StepError);
        Assert.Equal(AdminText.Required, SettingsForm.Validate("3", "25", " ").StepError);
    }

    [Theory]
    [InlineData("20", "20")]
    [InlineData("12.5", "12.5")]
    [InlineData("1,250.50", "1250.50")]
    [InlineData("$7", "7")]
    [InlineData("٠٫٧٥", "0.75")]
    public void Dollar_amounts_are_read_with_cents(string text, string expected)
    {
        var (value, error) = FormChecks.Amount(text);
        Assert.Null(error);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), value);
    }

    [Theory]
    [InlineData("12.505")] // more than 2 decimals
    [InlineData("-1")]
    [InlineData("twelve")]
    public void Wrong_dollar_amounts_are_explained(string text) => Assert.Equal(AdminText.AmountInvalid, FormChecks.Amount(text).Error);

    [Theory]
    [InlineData("11500", "11500")]
    [InlineData("11,500", "11500")]
    [InlineData(" ١١٥٠٠ ", "11500")]
    [InlineData("115.25", "115.25")]
    public void Exchange_rates_are_pounds_for_one_dollar(string text, string expected)
    {
        var (value, error) = FormChecks.Rate(text);
        Assert.Null(error);
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), value);
    }

    [Fact]
    public void An_exchange_rate_must_be_above_zero()
    {
        Assert.Equal(AdminText.RateInvalid, FormChecks.Rate("0").Error);
        Assert.Equal(AdminText.RateInvalid, FormChecks.Rate("-11500").Error);
        Assert.Equal(AdminText.RateInvalid, FormChecks.Rate("abc").Error);
        Assert.Equal(AdminText.Required, FormChecks.Rate("").Error);
        Assert.Null(FormChecks.Rate("").Value);
    }

    [Fact]
    public void Settings_form_explains_each_wrong_value()
    {
        var empty = SettingsForm.Validate("", " ");
        Assert.Equal((AdminText.Required, AdminText.Required), (empty.DaysError, empty.FeeError));

        var range = SettingsForm.Validate("61", "-5");
        Assert.False(range.IsValid);
        Assert.Equal("Enter a number from 0 to 60.", range.DaysError?.En);
        Assert.Equal("أدخل رقمًا من 0 إلى 60.", range.DaysError?.Ar);
        Assert.Equal(AdminText.AmountInvalid, range.FeeError);

        var text = SettingsForm.Validate("three", "a lot");
        Assert.Equal((AdminText.WholeNumber, AdminText.AmountInvalid), (text.DaysError, text.FeeError));
        Assert.Equal(GymSettingsLimit, SettingsForm.MaxExpiringSoonDays);
    }

    [Fact]
    public void Other_checks_trim_and_report()
    {
        Assert.Equal(("Omar", (LocalText?)null), FormChecks.Required("  Omar "));
        Assert.Equal(AdminText.Required, FormChecks.Required("   ").Error);
        Assert.Null(FormChecks.Optional("  "));
        Assert.Equal("x", FormChecks.Optional(" x "));
        Assert.Equal((12, (LocalText?)null), FormChecks.WholeNumber("12", 1, 120));
        Assert.NotNull(FormChecks.WholeNumber("0", 1, 120).Error);
    }

    [Theory]
    [InlineData(1, "1 month", "شهر واحد")]
    [InlineData(2, "2 months", "شهران")]
    [InlineData(3, "3 months", "3 أشهر")]
    [InlineData(10, "10 months", "10 أشهر")]
    [InlineData(12, "12 months", "12 شهرًا")]
    public void Months_use_the_arabic_number_forms(int months, string en, string ar)
    {
        Assert.Equal(en, AdminText.Months(months, AppLanguage.English));
        Assert.Equal(ar, AdminText.Months(months, AppLanguage.Arabic));
    }

    [Fact]
    public void Days_and_members_are_counted_in_both_languages()
    {
        Assert.Equal(("Today", "اليوم"), (AdminText.Days(0, AppLanguage.English), AdminText.Days(0, AppLanguage.Arabic)));
        Assert.Equal(("1 day", "يوم واحد"), (AdminText.Days(1, AppLanguage.English), AdminText.Days(1, AppLanguage.Arabic)));
        Assert.Equal("يومان", AdminText.Days(2, AppLanguage.Arabic));
        Assert.Equal(("30 days", "30 يومًا"), (AdminText.Days(30, AppLanguage.English), AdminText.Days(30, AppLanguage.Arabic)));
        Assert.Equal(("18 members", "18 مشتركًا"), (AdminText.MemberCount(18, AppLanguage.English), AdminText.MemberCount(18, AppLanguage.Arabic)));
        Assert.Equal("1,250 members", AdminText.MemberCount(1250, AppLanguage.English));
    }

    [Theory]
    [InlineData("0944123456", "0944 123 456")]
    [InlineData("+963944123456", "+963944123456")]
    [InlineData("011234567", "011234567")]
    [InlineData(null, "—")]
    public void Phones_are_grouped_for_reading(string? phone, string expected) => Assert.Equal(expected, PhoneText.Format(phone));

    [Fact]
    public void Sizes_and_backup_times()
    {
        Assert.Equal("3.4 MB", SizeText.Format(3_565_158, AppLanguage.English));
        Assert.Equal("3.4 ميغابايت", SizeText.Format(3_565_158, AppLanguage.Arabic));
        Assert.Equal("41 KB", SizeText.Format(41_692, AppLanguage.English));
        Assert.Equal("120 MB", SizeText.Format(120L * 1024 * 1024, AppLanguage.English));
        Assert.Equal("512 bytes", SizeText.Format(512, AppLanguage.English));

        var times = BackupTimes.Choices(new TimeOnly(2, 0));
        Assert.Equal(48, times.Count);
        Assert.Equal(new TimeOnly(0, 0), times[0]);
        Assert.Equal(new TimeOnly(23, 30), times[^1]);
        var odd = BackupTimes.Choices(new TimeOnly(3, 15, 40));
        Assert.Equal(49, odd.Count);
        Assert.Contains(new TimeOnly(3, 15), odd);
        Assert.Equal("03:15", BackupTimes.Format(new TimeOnly(3, 15)));
    }

    [Fact]
    public void Admin_error_codes_have_staff_messages()
    {
        var language = AppLanguage.Arabic;
        Assert.Equal(AdminErrors.LastAdmin.Ar, AdminErrors.Describe(new GymApiException(System.Net.HttpStatusCode.Conflict, "last_admin", "x"), language));
        Assert.Equal(AdminErrors.BackupFolderMissing.Ar, AdminErrors.Describe(new GymApiException(System.Net.HttpStatusCode.BadRequest, "backup_folder_missing", "x"), language));
        Assert.Equal(ErrorText.DuplicateBarcode.Ar, AdminErrors.Describe(new GymApiException(System.Net.HttpStatusCode.Conflict, "duplicate_barcode", "x"), language));
        Assert.Equal(AdminErrors.PgDumpFailed, AdminErrors.ForCode("pg_dump_failed"));
        Assert.Null(AdminErrors.ForCode("something_new"));
    }

    private static int GymSettingsLimit => Gym.Domain.GymSettings.MaxExpiringSoonDays;
}
