using Gym.Client;
using Gym.UI;

namespace Gym.Admin.Logic;

/// <summary>Staff messages for the server's error codes that only the Admin app meets; the rest go to <see cref="ErrorText"/>.</summary>
public static class AdminErrors
{
    public static LocalText LastAdmin { get; } = new(
        "This is the last admin login. Create another admin first.",
        "هذا آخر حساب مسؤول. أنشئ حساب مسؤول آخر أولًا.");

    public static LocalText RoleRequired { get; } = new("Choose a role for this login.", "اختر صلاحية لحساب الدخول هذا.");

    public static LocalText UsernameRequired { get; } = new("A login needs a username.", "حساب الدخول يحتاج إلى اسم مستخدم.");

    public static LocalText InvalidUsername { get; } = new(
        "Username: 3 to 32 characters, a-z, 0-9, dot, dash or underscore.",
        "اسم المستخدم: من 3 إلى 32 حرفًا لاتينيًا صغيرًا أو رقمًا أو نقطة أو شرطة.");

    public static LocalText InvalidPassword { get; } = new("The password needs 6 to 128 characters.", "كلمة المرور من 6 إلى 128 حرفًا.");

    public static LocalText NoLogin { get; } = new("Give this person a username first.", "أعطِ هذا الموظف اسم مستخدم أولًا.");

    public static LocalText NotClubTrainer { get; } = new("Only club trainers get a monthly rating.", "التقييم الشهري لمدربي النادي فقط.");

    public static LocalText InvalidStars { get; } = new("Choose 1 to 5 stars.", "اختر من نجمة إلى 5 نجوم.");

    public static LocalText NoteTooLong { get; } = new("The note is too long.", "الملاحظة طويلة جدًا.");

    public static LocalText InvalidMonth { get; } = new("This month cannot be rated.", "لا يمكن تقييم هذا الشهر.");

    public static LocalText TrainerHasClients { get; } = new(
        "This private trainer still has linked clients. Unlink them first (Reception app).",
        "لهذا المدرب الخاص متدربون مرتبطون به. افصلهم أولًا من برنامج الاستقبال.");

    public static LocalText InvalidSetting { get; } = new("A setting is out of range.", "قيمة أحد الإعدادات خارج المجال المسموح.");

    public static LocalText Validation { get; } = new("Check the fields: {0}", "تحقّق من الحقول: {0}");

    public static LocalText NegativeStock { get; } = new("The stock cannot go below zero.", "لا يمكن أن يصبح المخزون أقل من صفر.");

    public static LocalText BackupRunning { get; } = new("A backup is running right now. Try again in a moment.", "يجري نسخ احتياطي الآن. حاول بعد قليل.");

    public static LocalText BackupFolderMissing { get; } = new(
        "The backup folder is not there. Connect the backup drive or create the folder.",
        "مجلد النسخ الاحتياطي غير موجود. صِل قرص النسخ الاحتياطي أو أنشئ المجلد.");

    public static LocalText BackupFolderInvalid { get; } = new("Enter the full folder path, with the drive letter.", "أدخل المسار الكامل للمجلد مع حرف القرص.");

    public static LocalText BackupFolderNotWritable { get; } = new(
        "The server cannot write to this folder. Check the drive and its permissions.",
        "لا يستطيع الخادم الكتابة في هذا المجلد. تحقّق من القرص وصلاحياته.");

    public static LocalText BackupKeepInvalid { get; } = new("Keep 1 to 365 backups.", "احتفظ بعدد من 1 إلى 365 نسخة.");

    public static LocalText PgDumpNotFound { get; } = new(
        "pg_dump was not found on the server PC. Check its path.",
        "لم يُعثر على أداة pg_dump على جهاز الخادم. تحقّق من مسارها.");

    public static LocalText PgDumpFailed { get; } = new("The database backup tool reported an error.", "أبلغت أداة النسخ الاحتياطي لقاعدة البيانات عن خطأ.");

    public static LocalText BackupTimeout { get; } = new("The backup took too long and was stopped.", "استغرق النسخ الاحتياطي وقتًا طويلًا فأُوقف.");

    public static LocalText BackupFailed { get; } = new("The backup failed.", "فشل النسخ الاحتياطي.");

    private static readonly Dictionary<string, LocalText> ByCode = new(StringComparer.Ordinal)
    {
        ["last_admin"] = LastAdmin,
        ["role_required"] = RoleRequired,
        ["username_required"] = UsernameRequired,
        ["invalid_username"] = InvalidUsername,
        ["invalid_password"] = InvalidPassword,
        ["no_login"] = NoLogin,
        ["not_a_club_trainer"] = NotClubTrainer,
        ["invalid_stars"] = InvalidStars,
        ["note_too_long"] = NoteTooLong,
        ["invalid_month"] = InvalidMonth,
        ["trainer_has_clients"] = TrainerHasClients,
        ["invalid_setting"] = InvalidSetting,
        ["negative_stock"] = NegativeStock,
        ["backup_running"] = BackupRunning,
        ["backup_folder_missing"] = BackupFolderMissing,
        ["backup_folder_invalid"] = BackupFolderInvalid,
        ["backup_folder_not_writable"] = BackupFolderNotWritable,
        ["invalid_backup_keep"] = BackupKeepInvalid,
        ["pg_dump_not_found"] = PgDumpNotFound,
        ["pg_dump_failed"] = PgDumpFailed,
        ["backup_timeout"] = BackupTimeout,
        ["backup_failed"] = BackupFailed,
    };

    /// <summary>The message for an error code, or null when the code is not an Admin one.</summary>
    public static LocalText? ForCode(string? code) => code is not null && ByCode.TryGetValue(code, out var text) ? text : null;

    /// <summary>The message for <paramref name="error"/>: an Admin code first, then the shared <see cref="ErrorText"/>.</summary>
    public static string Describe(Exception error, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error switch
        {
            GymApiException { Code: "validation" } api => Validation.Format(language, api.Message),
            GymApiException { Code: { } code } when ForCode(code) is { } text => text.For(language),
            _ => ErrorText.Describe(error, language),
        };
    }

    /// <summary>The server's error code, or null.</summary>
    public static string? CodeOf(Exception error) => (error as GymApiException)?.Code;
}
