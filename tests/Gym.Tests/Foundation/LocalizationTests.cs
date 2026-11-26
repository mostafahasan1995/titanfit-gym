using System.Net;
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
        Assert.Equal("150,000 SYP", Money.Format(150_000, AppLanguage.English));
        var arabic = Money.Format(1_400_000, AppLanguage.Arabic);
        Assert.Contains("1,400,000", arabic, StringComparison.Ordinal);
        Assert.EndsWith("ل.س", arabic, StringComparison.Ordinal);
        Assert.DoesNotContain(arabic, c => c is >= '٠' and <= '٩');
        Assert.Equal("1,250", NumberText.Format(1250));
    }

    [Theory]
    [InlineData("150000", 150_000L)]
    [InlineData("150,000", 150_000L)]
    [InlineData(" 1,400,000 ", 1_400_000L)]
    [InlineData("١٥٠٬٠٠٠", 150_000L)]
    [InlineData("", null)]
    [InlineData("abc", null)]
    public void Typed_amounts_are_read(string text, long? expected)
    {
        Assert.Equal(expected, Money.Parse(text));
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
    public void Smoke_screenshot_files_are_named_app_page_language()
    {
        var app = new AppDefinition
        {
            App = GymApp.Reception,
            AppName = new LocalText("Reception", "الاستقبال"),
            NavLabel = new LocalText("Reception menu", "قائمة الاستقبال"),
            DefaultStationName = "Reception 1",
            Pages = [],
        };

        Assert.Equal("Reception-check-in-ar.png", SmokeScreenshot.FileName(app, "check-in", AppLanguage.Arabic));
        Assert.Equal("Reception-en.json", SmokeScreenshot.ManifestName(app, AppLanguage.English));
    }
}
