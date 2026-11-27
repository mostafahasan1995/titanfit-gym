using Gym.Contracts;
using Gym.Domain;
using Gym.Reception;
using Gym.Reception.Logic;
using Gym.UI;
using static Gym.Tests.Domain.Dates;

namespace Gym.Tests.Reception;

/// <summary>The check-in card texts, the renew date preview, counted words and the phone field.</summary>
public sealed class CheckInCardTests
{
    private static readonly DateTime At = new(2026, 11, 27, 16, 42, 0, DateTimeKind.Utc);

    [Fact]
    public void Active_is_green_with_the_days_left()
    {
        var card = CheckInCard.For(Result(CheckInResult.Active, daysLeft: 25), "18:42", AppLanguage.English);

        Assert.Equal(CardTone.Good, card.Tone);
        Assert.Equal("Access granted", card.Headline);
        Assert.Equal("Active · 25 days left", card.StatusText);
        Assert.Equal("25", card.DaysNumber);
        Assert.Equal("days left", card.DaysCaption);
        Assert.Equal("Entry logged at 18:42.", card.Note);
        Assert.False(card.ShowRenew);
    }

    [Fact]
    public void Expiring_soon_is_amber_with_the_days_to_go()
    {
        var card = CheckInCard.For(Result(CheckInResult.ExpiringSoon, daysLeft: 2), "18:42", AppLanguage.English);

        Assert.Equal(CardTone.Warning, card.Tone);
        Assert.Equal("Renew soon", card.Headline);
        Assert.Equal("Expires in 2 days", card.StatusText);
        Assert.False(card.ShowRenew);
    }

    [Fact]
    public void Expired_is_red_with_the_days_since_and_a_Renew_button()
    {
        var card = CheckInCard.For(Result(CheckInResult.Expired, daysOverdue: 6), "18:42", AppLanguage.English);

        Assert.Equal(CardTone.Bad, card.Tone);
        Assert.Equal("Access denied", card.Headline);
        Assert.Equal("Expired 6 days ago", card.StatusText);
        Assert.Equal("6", card.DaysNumber);
        Assert.Equal("days since expiry", card.DaysCaption);
        Assert.True(card.ShowRenew);
    }

    [Fact]
    public void Never_paid_is_red_with_Renew_and_unknown_codes_are_red_without()
    {
        var never = CheckInCard.For(Result(CheckInResult.NoSubscription), "18:42", AppLanguage.English);
        var unknown = CheckInCard.For(Result(CheckInResult.UnknownMember), "18:42", AppLanguage.English);

        Assert.Equal(CardTone.Bad, never.Tone);
        Assert.True(never.ShowRenew);
        Assert.Equal("—", never.DaysNumber);
        Assert.Equal(CardTone.Bad, unknown.Tone);
        Assert.False(unknown.ShowRenew);
        Assert.Equal("Not recognised", unknown.Headline);
    }

    [Theory]
    [InlineData(CheckInResult.ExpiringSoon, 0, 0, "Expires today", "ينتهي اليوم")]
    [InlineData(CheckInResult.ExpiringSoon, 1, 0, "Expires tomorrow", "ينتهي غدًا")]
    [InlineData(CheckInResult.ExpiringSoon, 2, 0, "Expires in 2 days", "ينتهي بعد يومين")]
    [InlineData(CheckInResult.ExpiringSoon, 3, 0, "Expires in 3 days", "ينتهي بعد 3 أيام")]
    [InlineData(CheckInResult.Active, 25, 0, "Active · 25 days left", "فعّال · متبقٍ 25 يومًا")]
    [InlineData(CheckInResult.Active, 10, 0, "Active · 10 days left", "فعّال · متبقٍ 10 أيام")]
    [InlineData(CheckInResult.Expired, 0, 1, "Expired yesterday", "انتهى أمس")]
    [InlineData(CheckInResult.Expired, 0, 2, "Expired 2 days ago", "انتهى منذ يومين")]
    [InlineData(CheckInResult.Expired, 0, 40, "Expired 40 days ago", "انتهى منذ 40 يومًا")]
    [InlineData(CheckInResult.NoSubscription, 0, 0, "No subscription yet", "لا يوجد اشتراك بعد")]
    public void Status_text_in_both_languages(CheckInResult result, int daysLeft, int daysOverdue, string english, string arabic)
    {
        Assert.Equal(english, CheckInCard.StatusText(result, daysLeft, daysOverdue, AppLanguage.English));
        Assert.Equal(arabic, CheckInCard.StatusText(result, daysLeft, daysOverdue, AppLanguage.Arabic));
    }

    [Fact]
    public void A_members_status_uses_the_same_words()
    {
        var expiring = new SubscriptionStatusDto(SubscriptionStatus.ExpiringSoon, D("2026-11-29"), 2, 0);

        Assert.Equal("Expires in 2 days", CheckInCard.StatusText(expiring, AppLanguage.English));
        Assert.Equal("No subscription yet", CheckInCard.StatusText(null, AppLanguage.English));
    }

    [Theory]
    [InlineData(1, "1 month", "شهر واحد")]
    [InlineData(2, "2 months", "شهران")]
    [InlineData(3, "3 months", "3 أشهر")]
    [InlineData(6, "6 months", "6 أشهر")]
    [InlineData(12, "12 months", "12 شهرًا")]
    [InlineData(100, "100 months", "100 شهر")]
    public void Months_are_counted_correctly(int months, string english, string arabic)
    {
        Assert.Equal(english, ReceptionFormat.Months(months, AppLanguage.English));
        Assert.Equal(arabic, ReceptionFormat.Months(months, AppLanguage.Arabic));
    }

    [Fact]
    public void Early_renewal_starts_the_day_after_the_current_end()
    {
        // PLAN example: 1 month bought on 26 Nov is valid up to 26 Dec; renewed on 20 Dec for 1 month: 27 Dec to 27 Jan.
        var preview = RenewPreview.For(D("2026-12-26"), D("2026-12-20"), 1);

        Assert.Equal(new PeriodPreview(D("2026-12-27"), D("2027-01-27"), ContinuesCurrent: true), preview);
    }

    [Fact]
    public void Renewal_after_expiry_or_a_first_payment_starts_today()
    {
        Assert.Equal(new PeriodPreview(D("2027-01-05"), D("2027-02-05"), false), RenewPreview.For(D("2026-12-26"), D("2027-01-05"), 1));
        Assert.Equal(new PeriodPreview(D("2026-11-27"), D("2027-02-27"), false), RenewPreview.For(null, D("2026-11-27"), 3));
    }

    [Fact]
    public void Renewal_on_the_last_day_and_month_ends_follow_the_server_rules()
    {
        Assert.Equal(D("2026-11-28"), RenewPreview.For(D("2026-11-27"), D("2026-11-27"), 1).Start);
        Assert.Equal(D("2028-02-29"), RenewPreview.For(null, D("2028-01-31"), 1).End); // leap year
        Assert.Equal(D("2027-02-28"), RenewPreview.For(null, D("2027-01-31"), 1).End);
    }

    [Theory]
    [InlineData("2026-11-01", "2026-11-30", "2026-11-15", 0.5)]
    [InlineData("2026-11-01", "2026-11-30", "2026-10-20", 0.0)]
    [InlineData("2026-11-01", "2026-11-30", "2026-12-20", 1.0)]
    public void Used_part_of_the_period(string start, string end, string today, double expected)
    {
        Assert.Equal(expected, RenewPreview.UsedFraction(D(start), D(end), D(today)), 2);
    }

    [Theory]
    [InlineData("944 123 456", "0944123456")]
    [InlineData("0944 123 456", "0944123456")]
    [InlineData("+963 944 123 456", "0944123456")]
    [InlineData("00963944123456", "0944123456")]
    [InlineData("٠٩٤٤١٢٣٤٥٦", "0944123456")]
    [InlineData("011 2233445", "0112233445")]
    [InlineData("", null)]
    [InlineData("abc", null)]
    public void The_phone_is_stored_like_the_existing_members(string typed, string? stored)
    {
        Assert.Equal(stored, ReceptionPhone.Normalize(typed));
    }

    [Fact]
    public void The_phone_is_shown_in_groups()
    {
        Assert.Equal("0944 123 456", ReceptionPhone.Format("0944123456"));
        Assert.Equal("944 123 456", ReceptionPhone.ForInput("+963944123456"));
        Assert.Equal("", ReceptionPhone.Format(null));
        Assert.True(ReceptionPhone.LooksLikeMobile("0944123456"));
        Assert.False(ReceptionPhone.LooksLikeMobile("0112233445"));
    }

    [Fact]
    public void Every_reception_string_has_English_and_Arabic()
    {
        var texts = LocalTextAudit.FindTexts(typeof(ReceptionText).Assembly);

        Assert.True(texts.Count > 200);
        Assert.Empty(LocalTextAudit.FindProblems(typeof(ReceptionText).Assembly));
    }

    private static CheckInResultDto Result(CheckInResult result, int daysLeft = 0, int daysOverdue = 0) => new(
        1, result, CheckInRules.IsAdmitted(result), At, "Reception 1", CheckInMethod.QrCode,
        result == CheckInResult.UnknownMember ? null : Guid.NewGuid(),
        result == CheckInResult.UnknownMember ? null : 231,
        result == CheckInResult.UnknownMember ? null : "#00231",
        result == CheckInResult.UnknownMember ? null : "Omar Al-Khatib",
        result == CheckInResult.UnknownMember ? null : MemberType.Regular,
        null,
        result is CheckInResult.NoSubscription or CheckInResult.UnknownMember ? null : D("2026-12-22"),
        daysLeft,
        daysOverdue,
        null);
}
