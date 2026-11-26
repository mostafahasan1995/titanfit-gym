using Gym.UI;

namespace Gym.Admin;

/// <summary>
/// Every string of the Admin app, in English and Arabic (docs/PLAN.md, "How to add strings").
/// XAML: <c>{ui:Loc {x:Static local:AdminText.Key}}</c>. Code: <c>AdminText.Key.For(UiLanguage.Current)</c>.
/// </summary>
public static class AdminText
{
    public static LocalText AppName { get; } = new("Admin", "الإدارة");

    public static LocalText NavLabel { get; } = new("Admin menu", "قائمة الإدارة");

    public static LocalText Dashboard { get; } = new("Dashboard", "لوحة التحكم");

    public static LocalText DashboardPlanned { get; } = new("Today's check-ins by hour, active, expiring and expired members, revenue, low stock and who expires soon.", "الحضور اليوم حسب الساعة، والمشتركون الفعّالون ومن ينتهي قريبًا والمنتهون، والإيرادات ونقص المخزون.");

    public static LocalText DashboardSample { get; } = new("Sample data: {0} check-ins today, {1} active members", "بيانات تجريبية: {0} عملية دخول اليوم، {1} مشتركًا فعّالًا");

    public static LocalText Members { get; } = new("Members", "المشتركون");

    public static LocalText MembersPlanned { get; } = new("All members with filters by status and type, editing and payment history.", "كل المشتركين مع التصفية حسب الحالة والنوع، والتعديل وسجل الدفعات.");

    public static LocalText MembersSample { get; } = new("Sample data: {0} members", "بيانات تجريبية: {0} مشتركًا");

    public static LocalText Plans { get; } = new("Plans & prices", "الخطط والأسعار");

    public static LocalText PlansPlanned { get; } = new("The 1, 3, 6 and 12 month plans and their prices.", "خطط 1 و3 و6 و12 شهرًا وأسعارها.");

    public static LocalText PlansSample { get; } = new("Sample data: {0} plans", "بيانات تجريبية: {0} خطط");

    public static LocalText CafeteriaItems { get; } = new("Cafeteria items", "أصناف الكافتيريا");

    public static LocalText CafeteriaItemsPlanned { get; } = new("Categories and products: names, prices, barcodes and minimum stock.", "الفئات والأصناف: الأسماء والأسعار والباركود والحد الأدنى للمخزون.");

    public static LocalText CafeteriaItemsSample { get; } = new("Sample data: {0} products in {1} categories", "بيانات تجريبية: {0} صنفًا في {1} فئات");

    public static LocalText Staff { get; } = new("Staff & ratings", "الموظفون والتقييمات");

    public static LocalText StaffPlanned { get; } = new("Staff, their logins and roles, and the monthly club trainer ratings.", "الموظفون وحسابات الدخول والصلاحيات، والتقييم الشهري لمدربي النادي.");

    public static LocalText StaffSample { get; } = new("Sample data: {0} staff, {1} club trainers", "بيانات تجريبية: {0} موظفين، منهم {1} مدربو نادٍ");

    public static LocalText Settings { get; } = new("Settings", "الإعدادات");

    public static LocalText SettingsPlanned { get; } = new("The expiring-soon threshold and the private training fee.", "حدّ التنبيه بقرب انتهاء الاشتراك ورسوم التدريب الخاص.");

    public static LocalText SettingsSample { get; } = new("Sample data: expiring soon = {0} days, private training {1} a month", "بيانات تجريبية: قرب الانتهاء = {0} أيام، التدريب الخاص {1} شهريًا");

    public static LocalText Reports { get; } = new("Reports", "التقارير");

    public static LocalText ReportsPlanned { get; } = new("Subscriptions, attendance and sales over any period.", "الاشتراكات والحضور والمبيعات لأي فترة.");

    public static LocalText ReportsSample { get; } = new("Sample data: cafeteria sales in the last 7 days {0}", "بيانات تجريبية: مبيعات الكافتيريا في آخر 7 أيام {0}");

    public static LocalText Backups { get; } = new("Backups", "النسخ الاحتياطي");

    public static LocalText BackupsPlanned { get; } = new("A daily automatic database backup to an external drive.", "نسخ احتياطي يومي تلقائي لقاعدة البيانات إلى قرص خارجي.");

    public static LocalText BackupsSample { get; } = new("Runs on the server PC (step 5)", "يعمل على جهاز الخادم (الخطوة 5)");
}
