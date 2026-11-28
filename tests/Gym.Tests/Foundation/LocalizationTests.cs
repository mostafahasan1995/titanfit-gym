using System.Net;
using Gym.Domain;
using Gym.Client;
using Gym.UI;
using Gym.UI.Shell;

namespace Gym.Tests.Foundation;

public sealed class LocalizationTests
{
    [Fact]
    public void Every_shared_string_has_English_and_Arabic()
    {
        var texts = LocalTextAudit.FindTexts(typeof(CommonText).Assembly);

        Assert.True(texts.Count > 100, $"Only {texts.Count} strings found.");
        Assert.Contains(texts, t => t.Name == "CommonText.SignIn");
        Assert.Contains(texts, t => t.Name == "ErrorText.InsufficientStock");
        Assert.Empty(LocalTextAudit.FindProblems(typeof(CommonText).Assembly));
    }

    [Fact]
    public void The_audit_finds_missing_untranslated_and_mismatched_strings()
    {
        Assert.Single(LocalTextAudit.Check("A", new LocalText("Save", "")));
        Assert.Single(LocalTextAudit.Check("B", new LocalText("Save", "Save")));
        Assert.Single(LocalTextAudit.Check("C", new LocalText("{0} items", "أصناف")));
        Assert.Empty(LocalTextAudit.Check("D", new LocalText("{0} items · {1}", "{0} أصناف · {1}")));
        Assert.Empty(LocalTextAudit.Check("E", new LocalText("EN", "عربي")));
    }

    [Fact]
    public void Money_has_Western_digits_and_the_currency()
    {
        Assert.Equal("150,000 SYP", Money.FormatSyp(150_000, AppLanguage.English));
        var arabic = Money.FormatSyp(1_400_000, AppLanguage.Arabic);
        Assert.Contains("1,400,000", arabic, StringComparison.Ordinal);
        Assert.EndsWith("ل.س", arabic, StringComparison.Ordinal);
        Assert.DoesNotContain(arabic, c => c is >= '٠' and <= '٩');
        Assert.Equal("1,250", NumberText.Format(1250));
    }

    [Fact]
    public void Dollars_have_the_sign_and_two_decimals()
    {
        Assert.Equal("$1,250.50", Money.Format(1250.5m, AppLanguage.English));
        Assert.Equal("$20.00", Money.Format(20m, AppLanguage.English));
        Assert.Equal("-$2.00", Money.Format(-2m, AppLanguage.English));
        Assert.Equal("\u200E$\u200E20.00\u200F", Money.Format(20m, AppLanguage.Arabic));
        Assert.Equal("\u200E-$\u200E2.00\u200F", Money.Format(-2m, AppLanguage.Arabic));
        Assert.Equal("12.50", Money.FormatAmount(12.5m));
        Assert.Equal("230,000 SYP", Money.Format(Currency.Syp, 230_000m, AppLanguage.English));
        Assert.Equal("$20.00 · 230,000 SYP", Money.FormatWithSyp(20m, 230_000m, AppLanguage.English));
        Assert.Equal("$20.00", Money.FormatWithSyp(20m, null, AppLanguage.English));
    }

    [Fact]
    public void A_dollar_amount_inside_an_Arabic_sentence_is_left_alone_by_FixNumbers()
    {
        var amount = Money.Format(1250.5m, AppLanguage.Arabic);
        Assert.Equal(amount, BidiText.FixNumbers(amount, AppLanguage.Arabic));
    }

    [Fact]
    public void Negative_amounts_keep_the_minus_before_the_number_in_Arabic()
    {
        Assert.Equal("-2,000 SYP", Money.FormatSyp(-2_000, AppLanguage.English));
        Assert.Equal("\u200E-\u200E2,000 ل.س", Money.FormatSyp(-2_000, AppLanguage.Arabic));
    }

    [Theory]
    [InlineData("الاستقبال 1", true)]
    [InlineData("سارة منصور", true)]
    [InlineData("Reception 1", false)]
    [InlineData("#00231", false)]
    [InlineData(null, false)]
    public void Data_text_without_Arabic_letters_is_shown_left_to_right(string? text, bool rightToLeft)
    {
        Assert.Equal(rightToLeft, BidiText.HasRtl(text));
    }

    [Theory]
    [InlineData("150000", 150_000L)]
    [InlineData("150,000", 150_000L)]
    [InlineData(" 1,400,000 ", 1_400_000L)]
    [InlineData("١٥٠٬٠٠٠", 150_000L)]
    [InlineData("", null)]
    [InlineData("abc", null)]
    [InlineData("12.5", null)]
    public void Typed_pounds_are_read(string text, long? expected)
    {
        Assert.Equal(expected, Money.ParseSyp(text));
        Assert.Equal(expected, Money.Parse(Currency.Syp, text));
    }

    [Theory]
    [InlineData("20", "20")]
    [InlineData("12.5", "12.5")]
    [InlineData("$1,250.75", "1250.75")]
    [InlineData("١٢٫٥", "12.5")]
    [InlineData("0.25", "0.25")]
    [InlineData("1.234", null)]
    [InlineData("", null)]
    [InlineData("abc", null)]
    public void Typed_dollars_are_read_with_up_to_two_decimals(string text, string? expected)
    {
        Assert.Equal(expected is null ? null : decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), Money.Parse(text));
    }

    [Fact]
    public void Dates_use_the_Syrian_month_names_in_Arabic()
    {
        var day = new DateOnly(2026, 11, 26);

        Assert.Equal("26 Nov 2026", DateText.ShortDate(day, AppLanguage.English));
        Assert.Equal("26 تشرين الثاني 2026", DateText.ShortDate(day, AppLanguage.Arabic));
        Assert.Equal("الخميس 26 تشرين الثاني 2026", DateText.LongDate(new DateTime(2026, 11, 26), AppLanguage.Arabic));
        Assert.Equal("كانون الثاني 2027", DateText.Month(2027, 1, AppLanguage.Arabic));
    }

    [Theory]
    [InlineData("en", AppLanguage.English)]
    [InlineData("AR", AppLanguage.Arabic)]
    [InlineData("arabic", AppLanguage.Arabic)]
    public void Language_codes_are_read(string code, AppLanguage expected)
    {
        Assert.Equal(expected, AppLanguages.Parse(code));
        Assert.Equal(expected, AppLanguages.FromArgs(["--smoke-screenshots", "out", "--lang", code]));
    }

    [Fact]
    public void No_language_argument_means_the_saved_language()
    {
        Assert.Null(AppLanguages.FromArgs(["--smoke-screenshots", "out"]));
        Assert.Equal("ar", AppLanguage.Arabic.ToCode());
    }

    [Fact]
    public void Server_errors_are_shown_in_the_staff_language()
    {
        var shortage = new GymApiException(HttpStatusCode.BadRequest, "insufficient_stock", "Not enough stock: Water");

        Assert.Equal(ErrorText.InsufficientStock.Ar, ErrorText.Describe(shortage, AppLanguage.Arabic));
        Assert.Equal(ErrorText.NotAllowed.En, ErrorText.Describe(new GymApiException(HttpStatusCode.Forbidden, "forbidden", "x"), AppLanguage.English));
        Assert.Equal(ErrorText.CannotReachServer.En, ErrorText.Describe(new HttpRequestException("refused"), AppLanguage.English));
        Assert.Equal("Odd rule", ErrorText.Describe(new GymApiException(HttpStatusCode.BadRequest, "odd_rule", "Odd rule"), AppLanguage.English));
        Assert.True(ErrorText.IsSessionExpired(new GymApiException(HttpStatusCode.Unauthorized, null, "x")));
        Assert.False(ErrorText.IsSessionExpired(shortage));
    }

    [Fact]
    public void Smoke_screenshot_files_are_named_app_page_language_screen()
    {
        var app = new AppDefinition
        {
            App = GymApp.Reception,
            AppName = new LocalText("Reception", "الاستقبال"),
            NavLabel = new LocalText("Reception menu", "قائمة الاستقبال"),
            DefaultStationName = "Reception 1",
            Pages = [],
        };

        Assert.Equal("Reception-check-in-ar-1366x768-125.png", SmokeScreenshot.FileName(app, "check-in", AppLanguage.Arabic, new SmokeScreen(1366, 768, 125)));
        Assert.Equal("Reception-en.json", SmokeScreenshot.ManifestName(app, AppLanguage.English));
    }
}
