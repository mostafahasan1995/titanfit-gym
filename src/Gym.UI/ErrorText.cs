using System.Net;
using System.Net.Http;
using Gym.Client;

namespace Gym.UI;

/// <summary>Turns an error from the API into a short message for the staff, in their language.</summary>
public static class ErrorText
{
    public static LocalText CannotReachServer { get; } = new(
        "Cannot reach the server. Check the network and the server PC.",
        "تعذّر الوصول إلى الخادم. تحقّق من الشبكة وجهاز الخادم.");

    public static LocalText TimedOut { get; } = new("The server took too long to answer.", "تأخر الخادم في الرد.");

    public static LocalText NotAllowed { get; } = new("You are not allowed to do this.", "ليست لديك صلاحية لهذا الإجراء.");

    public static LocalText NotFound { get; } = new("Not found. It may have been removed.", "غير موجود. ربما تمت إزالته.");

    public static LocalText Unexpected { get; } = new("Something went wrong: {0}", "حدث خطأ: {0}");

    public static LocalText InsufficientStock { get; } = new("Not enough stock.", "الكمية في المخزون غير كافية.");

    public static LocalText DuplicateNationalNo { get; } = new("A member with this national number already exists.", "يوجد مشترك بهذا الرقم الوطني.");

    public static LocalText DuplicateUsername { get; } = new("This username is already taken.", "اسم المستخدم مستخدم من قبل.");

    public static LocalText DuplicateBarcode { get; } = new("Another item has this barcode.", "يوجد صنف آخر بهذا الباركود.");

    public static LocalText NotAPrivateTrainer { get; } = new("This member is not a private trainer.", "هذا المشترك ليس مدربًا خاصًا.");

    public static LocalText WrongPassword { get; } = new("The current password is not right.", "كلمة المرور الحالية غير صحيحة.");

    public static LocalText CashTooLow { get; } = new("The cash received is less than the amount due.", "المبلغ المستلم أقل من المبلغ المطلوب.");

    public static LocalText EmptySale { get; } = new("Add at least one item.", "أضف صنفًا واحدًا على الأقل.");

    public static LocalText NoExchangeRate { get; } = new(
        "No exchange rate has been entered yet: take the payment in US dollars.",
        "لم يُدخَل سعر صرف بعد: اقبض المبلغ بالدولار.");

    public static LocalText InvalidExchangeRate { get; } = new(
        "Enter the exchange rate as the pounds for 1 dollar (more than 0).",
        "أدخل سعر الصرف بالليرة لكل 1 دولار (أكثر من 0).");

    public static LocalText ExchangeRateNotFound { get; } = new(
        "The exchange rate changed. Check the amount and try again.",
        "تغيّر سعر الصرف. تحقّق من المبلغ وأعد المحاولة.");

    public static LocalText PageFailed { get; } = new("This screen could not load: {0}", "تعذّر تحميل هذه الشاشة: {0}");

    private static readonly Dictionary<string, LocalText> ByCode = new(StringComparer.Ordinal)
    {
        ["invalid_login"] = CommonText.WrongLogin,
        ["insufficient_stock"] = InsufficientStock,
        ["duplicate_national_no"] = DuplicateNationalNo,
        ["duplicate_username"] = DuplicateUsername,
        ["duplicate_barcode"] = DuplicateBarcode,
        ["not_a_private_trainer"] = NotAPrivateTrainer,
        ["wrong_password"] = WrongPassword,
        ["cash_received_too_low"] = CashTooLow,
        ["empty_sale"] = EmptySale,
        ["no_exchange_rate"] = NoExchangeRate,
        ["invalid_exchange_rate"] = InvalidExchangeRate,
        ["exchange_rate_not_found"] = ExchangeRateNotFound,
    };

    /// <summary>The message for <paramref name="error"/> in <paramref name="language"/>.</summary>
    public static string Describe(Exception error, AppLanguage language)
    {
        ArgumentNullException.ThrowIfNull(error);
        return error switch
        {
            GymApiException { Code: { } code } when ByCode.TryGetValue(code, out var text) => text.For(language),
            GymApiException { StatusCode: HttpStatusCode.Forbidden } => NotAllowed.For(language),
            GymApiException { StatusCode: HttpStatusCode.NotFound } => NotFound.For(language),
            GymApiException { StatusCode: HttpStatusCode.Unauthorized } => CommonText.SessionExpired.For(language),
            GymApiException api => api.Message,
            HttpRequestException => CannotReachServer.For(language),
            TaskCanceledException or TimeoutException => TimedOut.For(language),
            _ => Unexpected.Format(language, error.Message),
        };
    }

    /// <summary>True when the error only means "not logged in any more" (the shell already goes back to the login).</summary>
    public static bool IsSessionExpired(Exception error) =>
        error is GymApiException { StatusCode: HttpStatusCode.Unauthorized };
}
