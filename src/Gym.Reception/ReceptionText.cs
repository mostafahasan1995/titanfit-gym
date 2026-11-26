using Gym.UI;

namespace Gym.Reception;

/// <summary>
/// Every string of the Reception app, in English and Arabic (docs/PLAN.md, "How to add strings").
/// XAML: <c>{ui:Loc {x:Static local:ReceptionText.Key}}</c>. Code: <c>ReceptionText.Key.For(UiLanguage.Current)</c>.
/// </summary>
public static class ReceptionText
{
    public static LocalText AppName { get; } = new("Reception", "الاستقبال");

    public static LocalText NavLabel { get; } = new("Reception menu", "قائمة الاستقبال");

    public static LocalText CheckIn { get; } = new("Check-in", "تسجيل الحضور");

    public static LocalText CheckInPlanned { get; } = new("Scan the member's QR code with the camera, or type the member number. A big card shows the photo, name and status.", "امسح رمز QR الخاص بالمشترك بالكاميرا، أو اكتب رقم المشترك. تظهر بطاقة كبيرة فيها الصورة والاسم والحالة.");

    public static LocalText CheckInSample { get; } = new("Sample data: {0} check-ins today", "بيانات تجريبية: {0} عملية دخول اليوم");

    public static LocalText NewMember { get; } = new("New member", "مشترك جديد");

    public static LocalText NewMemberPlanned { get; } = new("Scan the ID card, add the phone number, take a photo, choose the months and save. The member's QR code is created.", "امسح البطاقة الشخصية، أضف رقم الهاتف، التقط صورة، اختر عدد الأشهر واحفظ. يُنشأ رمز QR للمشترك.");

    public static LocalText NewMemberSample { get; } = new("Sample data: {0} plans to choose from", "بيانات تجريبية: {0} خطط للاختيار");

    public static LocalText Members { get; } = new("Members", "المشتركون");

    public static LocalText MembersTitle { get; } = new("Members · search and renew", "المشتركون · البحث والتجديد");

    public static LocalText MembersPlanned { get; } = new("Find a member by name, phone, national number or QR code, see the payments and renew.", "ابحث عن مشترك بالاسم أو الهاتف أو الرقم الوطني أو رمز QR، واطّلع على الدفعات وجدّد الاشتراك.");

    public static LocalText MembersSample { get; } = new("Sample data: {0} members", "بيانات تجريبية: {0} مشتركًا");

    public static LocalText PrivateTrainers { get; } = new("Private trainers", "المدربون الخاصون");

    public static LocalText PrivateTrainersPlanned { get; } = new("Private trainers, their linked clients and the private training service.", "المدربون الخاصون وعملاؤهم المرتبطون بهم وخدمة التدريب الخاص.");

    public static LocalText PrivateTrainersSample { get; } = new("Sample data: {0} private trainers with {1} clients", "بيانات تجريبية: {0} مدربين خاصين مع {1} عملاء");
}
