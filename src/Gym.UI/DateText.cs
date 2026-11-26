using System.Globalization;

namespace Gym.UI;

/// <summary>Date formatting for the UI: Syrian month names in Arabic, Western digits 0-9 in both languages.</summary>
public static class DateText
{
    private static readonly string[] ArabicMonths =
    [
        "كانون الثاني", "شباط", "آذار", "نيسان", "أيار", "حزيران",
        "تموز", "آب", "أيلول", "تشرين الأول", "تشرين الثاني", "كانون الأول",
    ];

    // Indexed by DayOfWeek (Sunday = 0).
    private static readonly string[] ArabicDays =
    [
        "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت",
    ];

    /// <summary>"Thursday 26 Nov 2026" or "الخميس 26 تشرين الثاني 2026".</summary>
    public static string LongDate(DateTime date, AppLanguage language) => language == AppLanguage.Arabic
        ? string.Create(CultureInfo.InvariantCulture, $"{ArabicDays[(int)date.DayOfWeek]} {date.Day} {ArabicMonths[date.Month - 1]} {date.Year}")
        : date.ToString("dddd d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"26 Nov 2026" or "26 تشرين الثاني 2026".</summary>
    public static string ShortDate(DateOnly date, AppLanguage language) => language == AppLanguage.Arabic
        ? string.Create(CultureInfo.InvariantCulture, $"{date.Day} {ArabicMonths[date.Month - 1]} {date.Year}")
        : date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>"Nov 2026" or "تشرين الثاني 2026".</summary>
    public static string Month(int year, int month, AppLanguage language) => language == AppLanguage.Arabic
        ? string.Create(CultureInfo.InvariantCulture, $"{ArabicMonths[month - 1]} {year}")
        : new DateTime(year, month, 1).ToString("MMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>Local time of a UTC timestamp from the server: "18:42".</summary>
    public static string Time(DateTime utc) => utc.ToLocalTime().ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Local date and time of a UTC timestamp: "26 Nov 2026 · 18:42".</summary>
    public static string DateAndTime(DateTime utc, AppLanguage language)
    {
        var local = utc.ToLocalTime();
        return $"{ShortDate(DateOnly.FromDateTime(local), language)} · {local.ToString("HH:mm", CultureInfo.InvariantCulture)}";
    }
}
