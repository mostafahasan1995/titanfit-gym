using Gym.UI;
using static Gym.UI.BidiText;

namespace Gym.Tests.Foundation;

/// <summary>Numbers inside Arabic sentences keep their left-to-right order (BidiText.FixNumbers and Ltr).</summary>
public sealed class BidiTextTests
{
    [Fact]
    public void A_phone_number_in_an_Arabic_sentence_is_kept_left_to_right()
    {
        var text = FixNumbers("أدخل الرقم كاملًا، مثل 0944 123 456.", AppLanguage.Arabic);

        Assert.Equal($"أدخل الرقم كاملًا، مثل {Ltr("0944 123 456")}.", text);
    }

    [Theory]
    [InlineData("+963 944 123 456", "+963 944 123 456")]
    [InlineData("#00412", "#00412")]
    [InlineData("1,400,000", "1,400,000")]
    [InlineData("18:42", "18:42")]
    public void Whole_number_runs_are_wrapped(string text, string run)
    {
        Assert.Equal(Ltr(run), FixNumbers(text, AppLanguage.Arabic));
    }

    [Fact]
    public void Several_numbers_are_wrapped_one_by_one()
    {
        Assert.Equal($"{Ltr("#00412")} · {Ltr("0944 123 456")}", FixNumbers("#00412 · 0944 123 456", AppLanguage.Arabic));
        Assert.Equal($"{Lrm}26{Rlm} تشرين الثاني {Lrm}2026{Rlm}", FixNumbers("26 تشرين الثاني 2026", AppLanguage.Arabic));
    }

    [Theory]
    [InlineData("TF1-7KQ2M9")]
    [InlineData("ABC123")]
    [InlineData("")]
    [InlineData("لا أرقام هنا")]
    public void Codes_and_text_without_number_runs_are_not_changed(string text)
    {
        Assert.Equal(text, FixNumbers(text, AppLanguage.Arabic));
    }

    [Fact]
    public void English_text_is_not_changed()
    {
        Assert.Equal("Enter the full number, e.g. 0944 123 456.", FixNumbers("Enter the full number, e.g. 0944 123 456.", AppLanguage.English));
    }

    [Fact]
    public void Fixing_twice_changes_nothing_more_and_money_is_left_alone()
    {
        var once = FixNumbers("مثل 0944 123 456 أو #00412", AppLanguage.Arabic);
        Assert.Equal(once, FixNumbers(once, AppLanguage.Arabic));

        var negative = Money.FormatSyp(-2_000, AppLanguage.Arabic);
        Assert.Equal(negative, FixNumbers(negative, AppLanguage.Arabic));
    }

    [Fact]
    public void Ltr_marks_every_sign_and_space_between_the_digits()
    {
        Assert.Equal($"{Lrm}0944{Lrm} {Lrm}123{Lrm} {Lrm}456{Rlm}", Ltr("0944 123 456"));
        Assert.Equal($"{Lrm}{Lrm}#{Lrm}00412{Rlm}", Ltr("#00412"));
        Assert.Equal("", Ltr(null));
    }
}
