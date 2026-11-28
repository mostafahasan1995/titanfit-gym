using Gym.UI;

namespace Gym.Admin;

/// <summary>
/// The Admin app's sidebar and the words several pages share, in English and Arabic (docs/PLAN.md, "How to add strings").
/// Each page has its own strings class next to it (DashboardText, MembersText, ...).
/// XAML: <c>{ui:Loc {x:Static local:AdminText.Key}}</c>. Code: <c>AdminText.Key.For(Context.Language)</c>.
/// </summary>
public static class AdminText
{
    public static LocalText AppName { get; } = new("Admin", "الإدارة");

    public static LocalText NavLabel { get; } = new("Admin menu", "قائمة الإدارة");

    public static LocalText Dashboard { get; } = new("Dashboard", "لوحة التحكم");

    public static LocalText ExchangeRate { get; } = new("Exchange rate", "سعر الصرف");

    public static LocalText Members { get; } = new("Members", "المشتركون");

    public static LocalText Plans { get; } = new("Plans & prices", "الخطط والأسعار");

    public static LocalText CafeteriaItems { get; } = new("Cafeteria items", "أصناف الكافتيريا");

    public static LocalText Staff { get; } = new("Staff & ratings", "الموظفون والتقييمات");

    public static LocalText Settings { get; } = new("Settings", "الإعدادات");

    public static LocalText Reports { get; } = new("Reports", "التقارير");

    public static LocalText Backups { get; } = new("Backups", "النسخ الاحتياطي");

    // Shared words

    public static LocalText Active { get; } = new("Active", "فعّال");

    public static LocalText Inactive { get; } = new("Inactive", "غير فعّال");

    public static LocalText Name { get; } = new("Name", "الاسم");

    public static LocalText Phone { get; } = new("Phone", "الهاتف");

    public static LocalText Price { get; } = new("Price", "السعر");

    public static LocalText Total { get; } = new("Total", "الإجمالي");

    public static LocalText Count { get; } = new("Count", "العدد");

    public static LocalText Date { get; } = new("Date", "التاريخ");

    public static LocalText Status { get; } = new("Status", "الحالة");

    public static LocalText Note { get; } = new("Note", "ملاحظة");

    public static LocalText Saved { get; } = new("Saved.", "تم الحفظ.");

    public static LocalText Refresh { get; } = new("Refresh", "تحديث");

    public static LocalText NoneYet { get; } = new("None yet", "لا يوجد بعد");

    public static LocalText Dash { get; } = new("—", "—");

    public static LocalText WholeNumber { get; } = new("Enter a whole number.", "أدخل عددًا صحيحًا.");

    public static LocalText NumberRange { get; } = new("Enter a number from {0} to {1}.", "أدخل رقمًا من {0} إلى {1}.");

    public static LocalText Required { get; } = new("Required.", "حقل مطلوب.");

    public static LocalText AmountInvalid { get; } = new("Enter an amount in US dollars, e.g. 20 or 12.50.", "أدخل مبلغًا بالدولار، مثل 20 أو 12.50.");

    public static LocalText RateInvalid { get; } = new("Enter the Syrian pounds for 1 dollar, a number above 0, e.g. 11500.", "أدخل عدد الليرات السورية لكل 1 دولار، رقمًا أكبر من 0، مثل 11500.");

    // Counted words. Arabic: 1, 2, 3 to 10 and 11 and more each have their own form.

    public static LocalText MonthOne { get; } = new("1 month", "شهر واحد");

    public static LocalText MonthTwo { get; } = new("2 months", "شهران");

    public static LocalText MonthsFew { get; } = new("{0} months", "{0} أشهر");

    public static LocalText MonthsMany { get; } = new("{0} months", "{0} شهرًا");

    public static LocalText DayToday { get; } = new("Today", "اليوم");

    public static LocalText DayOne { get; } = new("1 day", "يوم واحد");

    public static LocalText DayTwo { get; } = new("2 days", "يومان");

    public static LocalText DaysFew { get; } = new("{0} days", "{0} أيام");

    public static LocalText DaysMany { get; } = new("{0} days", "{0} يومًا");

    public static LocalText MemberOne { get; } = new("1 member", "مشترك واحد");

    public static LocalText MemberTwo { get; } = new("2 members", "مشتركان");

    public static LocalText MembersFew { get; } = new("{0} members", "{0} مشتركين");

    public static LocalText MembersMany { get; } = new("{0} members", "{0} مشتركًا");

    /// <summary>"3 months" / "3 أشهر" (the name of a plan).</summary>
    public static string Months(int months, AppLanguage language) => Counted(months, MonthOne, MonthTwo, MonthsFew, MonthsMany, language);

    /// <summary>"Today", "1 day", "5 days" / "اليوم"، "يوم واحد"، "5 أيام".</summary>
    public static string Days(int days, AppLanguage language) =>
        days == 0 ? DayToday.For(language) : Counted(days, DayOne, DayTwo, DaysFew, DaysMany, language);

    public static string MemberCount(int count, AppLanguage language) => Counted(count, MemberOne, MemberTwo, MembersFew, MembersMany, language);

    /// <summary>Picks the form for <paramref name="count"/>: English 1 / other; Arabic 1, 2, 3-10, 11+ (and 0 like 11+).</summary>
    public static string Counted(int count, LocalText one, LocalText two, LocalText few, LocalText many, AppLanguage language)
    {
        var abs = Math.Abs(count);
        var text = abs switch
        {
            1 => one,
            2 => two,
            >= 3 and <= 10 => few,
            _ => many,
        };
        return text.Format(language, NumberText.Format(count));
    }
}
