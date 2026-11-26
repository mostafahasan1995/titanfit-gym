namespace Gym.UI;

/// <summary>
/// Strings shared by every app: buttons, login, server settings, connection status, roles, statuses.
/// App strings live in the app (e.g. <c>Gym.Reception/ReceptionText.cs</c>) in the same form.
/// </summary>
public static class CommonText
{
    // Buttons and common words

    public static LocalText Ok { get; } = new("OK", "موافق");

    public static LocalText Cancel { get; } = new("Cancel", "إلغاء");

    public static LocalText Save { get; } = new("Save", "حفظ");

    public static LocalText Close { get; } = new("Close", "إغلاق");

    public static LocalText Yes { get; } = new("Yes", "نعم");

    public static LocalText No { get; } = new("No", "لا");

    public static LocalText Retry { get; } = new("Try again", "إعادة المحاولة");

    public static LocalText Search { get; } = new("Search", "بحث");

    public static LocalText Clear { get; } = new("Clear", "مسح");

    public static LocalText Add { get; } = new("Add", "إضافة");

    public static LocalText Edit { get; } = new("Edit", "تعديل");

    public static LocalText Delete { get; } = new("Delete", "حذف");

    public static LocalText Confirm { get; } = new("Confirm", "تأكيد");

    public static LocalText Loading { get; } = new("Loading…", "جارٍ التحميل…");

    public static LocalText PleaseWait { get; } = new("Please wait…", "يرجى الانتظار…");

    public static LocalText SearchPlaceholder { get; } = new("Search…", "بحث…");

    public static LocalText PickDate { get; } = new("Pick a date", "اختر تاريخًا");

    public static LocalText NothingHere { get; } = new("Nothing here yet", "لا يوجد شيء بعد");

    public static LocalText NoResults { get; } = new("No results", "لا توجد نتائج");

    public static LocalText NoResultsText { get; } = new("Try another name, phone or number.", "جرّب اسمًا أو رقم هاتف أو رقمًا آخر.");

    // Shell

    public static LocalText LogOut { get; } = new("Log out", "تسجيل الخروج");

    public static LocalText Language { get; } = new("Language", "اللغة");

    public static LocalText Station { get; } = new("Station", "المحطة");

    public static LocalText NotSignedIn { get; } = new("Not signed in", "لم يتم تسجيل الدخول");

    public static LocalText ServerNotConnected { get; } = new("Server not connected", "الخادم غير متصل");

    public static LocalText ComingSoonTitle { get; } = new("This screen is coming next", "هذه الشاشة قادمة قريبًا");

    public static LocalText ComingSoonText { get; } = new(
        "The layout, login and sample data are in place. This screen is built in the next step.",
        "التخطيط وتسجيل الدخول والبيانات التجريبية جاهزة. تُبنى هذه الشاشة في الخطوة التالية.");

    // Live connection (header status light)

    public static LocalText LiveConnected { get; } = new("Server online", "الخادم متصل");

    public static LocalText LiveConnecting { get; } = new("Connecting…", "جارٍ الاتصال…");

    public static LocalText LiveReconnecting { get; } = new("Reconnecting…", "جارٍ إعادة الاتصال…");

    public static LocalText LiveOffline { get; } = new("Server offline", "الخادم غير متصل");

    // Login

    public static LocalText SignInTitle { get; } = new("Sign in", "تسجيل الدخول");

    public static LocalText SignInText { get; } = new("Use your staff username and password.", "استخدم اسم المستخدم وكلمة المرور الخاصة بك.");

    public static LocalText Username { get; } = new("Username", "اسم المستخدم");

    public static LocalText Password { get; } = new("Password", "كلمة المرور");

    public static LocalText SignIn { get; } = new("Sign in", "دخول");

    public static LocalText SigningIn { get; } = new("Signing in…", "جارٍ تسجيل الدخول…");

    public static LocalText EnterUsernameAndPassword { get; } = new("Enter your username and password.", "أدخل اسم المستخدم وكلمة المرور.");

    public static LocalText WrongLogin { get; } = new("Wrong username or password.", "اسم المستخدم أو كلمة المرور غير صحيحة.");

    public static LocalText SessionExpired { get; } = new("Your session ended. Please sign in again.", "انتهت الجلسة. سجّل الدخول من جديد.");

    public static LocalText SignInAgainNewServer { get; } = new("The server changed. Please sign in again.", "تغيّر الخادم. سجّل الدخول من جديد.");

    public static LocalText ServerLabel { get; } = new("Server", "الخادم");

    public static LocalText ServerUnreachable { get; } = new(
        "Cannot reach the server. Check the address in Server settings.",
        "تعذّر الوصول إلى الخادم. تحقّق من العنوان في إعدادات الخادم.");

    public static LocalText ReceptionOnly { get; } = new("This app is for reception staff and admins.", "هذا البرنامج لموظفي الاستقبال والمسؤولين فقط.");

    public static LocalText CafeteriaOnly { get; } = new("This app is for cashiers and admins.", "هذا البرنامج للكاشير والمسؤولين فقط.");

    public static LocalText AdminOnly { get; } = new("This app is for admins only.", "هذا البرنامج للمسؤولين فقط.");

    // First run: the first admin

    public static LocalText FirstAdminTitle { get; } = new("Create the first admin", "إنشاء حساب المسؤول الأول");

    public static LocalText FirstAdminText { get; } = new(
        "The server has no admin yet. Create the admin account to start.",
        "لا يوجد مسؤول على الخادم بعد. أنشئ حساب المسؤول للبدء.");

    public static LocalText NoAdminYet { get; } = new(
        "The server has no admin yet. Open the Admin app on the server PC to create one.",
        "لا يوجد مسؤول على الخادم بعد. افتح برنامج الإدارة على جهاز الخادم لإنشائه.");

    public static LocalText FullName { get; } = new("Full name", "الاسم الكامل");

    public static LocalText ConfirmPassword { get; } = new("Confirm password", "تأكيد كلمة المرور");

    public static LocalText CreateAdmin { get; } = new("Create admin", "إنشاء المسؤول");

    public static LocalText PasswordsDiffer { get; } = new("The passwords do not match.", "كلمتا المرور غير متطابقتين.");

    public static LocalText FillAllFields { get; } = new("Fill in all fields.", "املأ جميع الحقول.");

    // Server settings (station.json)

    public static LocalText ServerSettings { get; } = new("Server settings", "إعدادات الخادم");

    public static LocalText FirstRunText { get; } = new(
        "Welcome to TITANFIT. Enter the address of the server (the admin PC).",
        "مرحبًا بك في TITANFIT. أدخل عنوان الخادم (جهاز الإدارة).");

    public static LocalText ServerAddress { get; } = new("Server address", "عنوان الخادم");

    public static LocalText ServerAddressHint { get; } = new("The admin PC, e.g. http://192.168.1.10:5080", "عنوان جهاز الإدارة، مثل 192.168.1.10");

    public static LocalText StationName { get; } = new("Station name", "اسم المحطة");

    public static LocalText StationNameHint { get; } = new(
        "Shown in the check-in log and on receipts, e.g. Reception 1.",
        "يظهر في سجل الحضور وعلى الإيصالات، مثل الاستقبال 1.");

    public static LocalText TestConnection { get; } = new("Test connection", "اختبار الاتصال");

    public static LocalText Testing { get; } = new("Testing…", "جارٍ الاختبار…");

    public static LocalText ConnectionOk { get; } = new("Connected · server version {0}", "متصل · إصدار الخادم {0}");

    public static LocalText ConnectionFailed { get; } = new("No connection: {0}", "لا يوجد اتصال: {0}");

    public static LocalText InvalidServerAddress { get; } = new("Enter a valid address, e.g. http://192.168.1.10:5080", "أدخل عنوانًا صحيحًا، مثل 192.168.1.10");

    public static LocalText SettingsSaved { get; } = new("Settings saved.", "تم حفظ الإعدادات.");

    public static LocalText SettingsSaveFailed { get; } = new("Could not save the settings: {0}", "تعذّر حفظ الإعدادات: {0}");

    public static LocalText SettingsFile { get; } = new("Saved on this PC in {0}", "تُحفظ على هذا الجهاز في {0}");

    // Roles

    public static LocalText RoleAdmin { get; } = new("Administrator", "مسؤول النظام");

    public static LocalText RoleReception { get; } = new("Receptionist", "موظف استقبال");

    public static LocalText RoleCashier { get; } = new("Cashier", "كاشير");

    // Subscription status (always with an icon and a label, never colour alone)

    public static LocalText StatusActive { get; } = new("Active", "فعّال");

    public static LocalText StatusExpiring { get; } = new("Expiring", "ينتهي قريبًا");

    public static LocalText StatusExpired { get; } = new("Expired", "منتهٍ");

    public static LocalText StatusNone { get; } = new("No subscription", "بلا اشتراك");

    // Money

    public static LocalText Currency { get; } = new("SYP", "ل.س");
}
