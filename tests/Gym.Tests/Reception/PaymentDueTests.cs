using Gym.Domain;
using Gym.Reception.Logic;
using Gym.UI;

namespace Gym.Tests.Reception;

/// <summary>The amount reception asks for: dollars, or pounds at the day's rate (each payment rounded on its own).</summary>
public sealed class PaymentDueTests
{
    [Fact]
    public void Dollars_are_the_sum_of_the_prices()
    {
        var due = PaymentDue.For([20m, 25m], Currency.Usd, 11_500m, 1);

        Assert.Equal(Currency.Usd, due.Currency);
        Assert.Equal(45m, due.Amount);
        Assert.Equal(11_500m, due.SypPerUsd);
    }

    [Fact]
    public void Pounds_convert_and_round_each_payment_then_add_them()
    {
        // 1.25 × 11,333 = 14,166.25 and 0.75 × 11,333 = 8,499.75: rounded to 500 each, 14,000 + 8,500.
        var due = PaymentDue.For([1.25m, 0.75m], Currency.Syp, 11_333m, 500);

        Assert.Equal(Currency.Syp, due.Currency);
        Assert.Equal(22_500m, due.Amount);
        Assert.Equal(11_333m, due.SypPerUsd);
    }

    [Fact]
    public void Pounds_without_a_rate_stay_in_dollars()
    {
        var due = PaymentDue.For([20m], Currency.Syp, null, 1);

        Assert.Equal(Currency.Usd, due.Currency);
        Assert.Equal(20m, due.Amount);
        Assert.Null(due.SypPerUsd);
    }

    [Fact]
    public void The_line_under_the_amount_shows_the_other_currency()
    {
        Assert.Equal("That is 230,000 SYP", PaymentDue.OtherLine(20m, new PaymentAmount(Currency.Usd, 20m, 11_500m), 1, AppLanguage.English));
        Assert.Equal("That is $20.00", PaymentDue.OtherLine(20m, new PaymentAmount(Currency.Syp, 230_000m, 11_500m), 1, AppLanguage.English));
        Assert.Equal("", PaymentDue.OtherLine(20m, new PaymentAmount(Currency.Usd, 20m, null), 1, AppLanguage.English));
    }

    [Fact]
    public void A_payment_in_pounds_shows_the_pounds_and_the_rate()
    {
        Assert.Equal("230,000 SYP", PaymentDue.PaidLine(Currency.Syp, 230_000m, AppLanguage.English));
        Assert.Equal("", PaymentDue.PaidLine(Currency.Usd, 20m, AppLanguage.English));
        Assert.Equal("Paid 230,000 SYP at $1 = 11,500 SYP", PaymentDue.PaidDetail(Currency.Syp, 230_000m, 11_500m, AppLanguage.English));
        Assert.Null(PaymentDue.PaidDetail(Currency.Usd, 20m, 11_500m, AppLanguage.English));
    }

    [Fact]
    public void Arabic_lines_keep_the_numbers_in_order()
    {
        var line = PaymentDue.OtherLine(20m, new PaymentAmount(Currency.Usd, 20m, 11_500m), 1, AppLanguage.Arabic);

        Assert.StartsWith("أي ما يعادل ", line, StringComparison.Ordinal);
        Assert.Contains(BidiText.Ltr("230,000"), line, StringComparison.Ordinal);
        Assert.EndsWith("ل.س", line, StringComparison.Ordinal);
    }
}
