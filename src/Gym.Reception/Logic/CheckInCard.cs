using Gym.Contracts;
using Gym.Domain;
using Gym.UI;

namespace Gym.Reception.Logic;

/// <summary>The colour of the check-in card. Always shown together with an icon and a label.</summary>
public enum CardTone
{
    /// <summary>No scan yet.</summary>
    Waiting,

    /// <summary>Green: active.</summary>
    Good,

    /// <summary>Amber: admitted, expires within the threshold from the settings.</summary>
    Warning,

    /// <summary>Red: expired, never paid or unknown code.</summary>
    Bad,
}

/// <summary>What the big check-in card says for one result, in one language.</summary>
/// <param name="Headline">"Access granted", "Renew soon", "Access denied", "Not recognised".</param>
/// <param name="StatusText">"Active · 25 days left", "Expires in 2 days", "Expired 6 days ago", …</param>
/// <param name="DaysNumber">The big number: days left, or days since the end date; "—" when there is none.</param>
/// <param name="DaysCaption">Under the big number: "days left" or "days since expiry".</param>
/// <param name="Note">The line at the bottom of the card.</param>
/// <param name="ShowRenew">The gold Renew button (expired or never paid).</param>
public sealed record CheckInCardModel(
    CardTone Tone,
    string Headline,
    string StatusText,
    string DaysNumber,
    string DaysCaption,
    string Note,
    bool ShowRenew);

/// <summary>Turns a check-in result into the card text (green / amber / red, docs/PLAN.md "Check-in").</summary>
public static class CheckInCard
{
    public static CardTone ToneOf(CheckInResult result) => result switch
    {
        CheckInResult.Active => CardTone.Good,
        CheckInResult.ExpiringSoon => CardTone.Warning,
        _ => CardTone.Bad,
    };

    /// <summary>The status line: "Active · 25 days left", "Expires in 2 days", "Expires today", "Expired 6 days ago".</summary>
    public static string StatusText(CheckInResult result, int daysLeft, int daysOverdue, AppLanguage language) => result switch
    {
        CheckInResult.Active => ReceptionText.StatusActiveDays.Format(language, ReceptionFormat.Days(daysLeft, language)),
        CheckInResult.ExpiringSoon => daysLeft switch
        {
            <= 0 => ReceptionText.StatusExpiresToday.For(language),
            1 => ReceptionText.StatusExpiresTomorrow.For(language),
            _ => ReceptionText.StatusExpiresIn.Format(language, ReceptionFormat.Days(daysLeft, language, ArabicCase.Genitive)),
        },
        CheckInResult.Expired => daysOverdue <= 1
            ? ReceptionText.StatusExpiredYesterday.For(language)
            : ReceptionText.StatusExpiredAgo.Format(language, ReceptionFormat.Days(daysOverdue, language, ArabicCase.Genitive)),
        CheckInResult.NoSubscription => ReceptionText.StatusNoSubscription.For(language),
        _ => ReceptionText.StatusUnknownCode.For(language),
    };

    /// <summary>The same status line for a member's membership (members list, details), or "No subscription".</summary>
    public static string StatusText(SubscriptionStatusDto? membership, AppLanguage language) => membership switch
    {
        null => ReceptionText.StatusNoSubscription.For(language),
        { Status: SubscriptionStatus.Active } active => StatusText(CheckInResult.Active, active.DaysLeft, 0, language),
        { Status: SubscriptionStatus.ExpiringSoon } expiring => StatusText(CheckInResult.ExpiringSoon, expiring.DaysLeft, 0, language),
        { } expired => StatusText(CheckInResult.Expired, 0, expired.DaysOverdue, language),
    };

    /// <param name="localTime">When the check-in happened, local time "18:42" (for the note of an admitted member).</param>
    public static CheckInCardModel For(CheckInResultDto result, string localTime, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(result);
        var status = StatusText(result.Result, result.DaysLeft, result.DaysOverdue, language);
        return result.Result switch
        {
            CheckInResult.Active => new CheckInCardModel(
                CardTone.Good,
                ReceptionText.HeadlineGranted.For(language),
                status,
                result.DaysLeft.ToString(System.Globalization.CultureInfo.InvariantCulture),
                (result.DaysLeft == 1 ? ReceptionText.DayLeftCaption : ReceptionText.DaysLeftCaption).For(language),
                ReceptionText.NoteGranted.Format(language, localTime),
                false),
            CheckInResult.ExpiringSoon => new CheckInCardModel(
                CardTone.Warning,
                ReceptionText.HeadlineRenewSoon.For(language),
                status,
                result.DaysLeft.ToString(System.Globalization.CultureInfo.InvariantCulture),
                (result.DaysLeft == 1 ? ReceptionText.DayLeftCaption : ReceptionText.DaysLeftCaption).For(language),
                ReceptionText.NoteRenewSoon.For(language),
                false),
            CheckInResult.Expired => new CheckInCardModel(
                CardTone.Bad,
                ReceptionText.HeadlineDenied.For(language),
                status,
                result.DaysOverdue.ToString(System.Globalization.CultureInfo.InvariantCulture),
                (result.DaysOverdue == 1 ? ReceptionText.DayOverdueCaption : ReceptionText.DaysOverdueCaption).For(language),
                ReceptionText.NoteExpired.For(language),
                true),
            CheckInResult.NoSubscription => new CheckInCardModel(
                CardTone.Bad,
                ReceptionText.HeadlineDenied.For(language),
                status,
                "—",
                "",
                ReceptionText.NoteNoSubscription.For(language),
                true),
            _ => new CheckInCardModel(
                CardTone.Bad,
                ReceptionText.HeadlineUnknown.For(language),
                status,
                "—",
                "",
                ReceptionText.NoteUnknown.For(language),
                false),
        };
    }

    /// <summary>The short pill label of a recent check-in: Active / Expiring / Expired / Unknown.</summary>
    public static LocalText ShortLabel(CheckInResult result) => result switch
    {
        CheckInResult.Active => CommonText.StatusActive,
        CheckInResult.ExpiringSoon => CommonText.StatusExpiring,
        CheckInResult.Expired => CommonText.StatusExpired,
        CheckInResult.NoSubscription => CommonText.StatusNone,
        _ => ReceptionText.ShortUnknown,
    };
}
