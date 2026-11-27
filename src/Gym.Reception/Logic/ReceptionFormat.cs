using Gym.UI;

namespace Gym.Reception.Logic;

/// <summary>The grammatical case of a counted noun in Arabic: "يومان" after a verb, "يومين" after بعد / منذ.</summary>
public enum ArabicCase
{
    /// <summary>"متبقٍ يومان".</summary>
    Nominative,

    /// <summary>"بعد يومين", "منذ يومين".</summary>
    Genitive,
}

/// <summary>Counted days and months in English and Arabic (one, two, 3 to 10, 11 and up follow different Arabic forms).</summary>
public static class ReceptionFormat
{
    /// <summary>"1 day", "25 days" / "يوم واحد", "يومان", "6 أيام", "25 يومًا".</summary>
    public static string Days(int count, AppLanguage language, ArabicCase arabicCase = ArabicCase.Nominative) => Counted(
        count, language, arabicCase,
        ReceptionText.DayOne, ReceptionText.DayTwo, ReceptionText.DayTwoGenitive, ReceptionText.DaysFew, ReceptionText.DaysMany, ReceptionText.DaysHundred);

    /// <summary>"1 month", "3 months" / "شهر واحد", "شهران", "3 أشهر", "12 شهرًا".</summary>
    public static string Months(int count, AppLanguage language, ArabicCase arabicCase = ArabicCase.Nominative) => Counted(
        count, language, arabicCase,
        ReceptionText.MonthOne, ReceptionText.MonthTwo, ReceptionText.MonthTwoGenitive, ReceptionText.MonthsFew, ReceptionText.MonthsMany, ReceptionText.MonthsHundred);

    private static string Counted(
        int count, AppLanguage language, ArabicCase arabicCase,
        LocalText one, LocalText two, LocalText twoGenitive, LocalText few, LocalText many, LocalText hundred)
    {
        if (count == 1)
        {
            return one.For(language);
        }

        if (count == 2)
        {
            return (arabicCase == ArabicCase.Genitive ? twoGenitive : two).For(language);
        }

        var rest = count % 100;
        var text = rest is >= 3 and <= 10 ? few : rest is >= 11 and <= 99 ? many : hundred;
        return text.Format(language, count);
    }
}
