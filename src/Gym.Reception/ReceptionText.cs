using Gym.UI;

namespace Gym.Reception;

/// <summary>
/// Every string of the Reception app, in English and Arabic (docs/PLAN.md, "How to add strings").
/// XAML: <c>{ui:Loc {x:Static local:ReceptionText.CheckIn}}</c>. Code: <c>ReceptionText.CheckIn.For(Context.Language)</c>.
/// Text with numbers in groups goes through <c>BidiText.FixNumbers</c> before it is shown.
/// </summary>
public static class ReceptionText
{
    // App and pages

    public static LocalText AppName { get; } = new("Reception", "الاستقبال");

    public static LocalText NavLabel { get; } = new("Reception menu", "قائمة الاستقبال");

    public static LocalText CheckIn { get; } = new("Check-in", "تسجيل الحضور");

    public static LocalText NewMember { get; } = new("New member", "مشترك جديد");

    public static LocalText Members { get; } = new("Members", "المشتركون");

    public static LocalText MembersTitle { get; } = new("Members · search and renew", "المشتركون · البحث والتجديد");

    public static LocalText PrivateTrainers { get; } = new("Private trainers", "المدربون الخاصون");

    // Counted days and months (ReceptionFormat)

    public static LocalText DayOne { get; } = new("1 day", "يوم واحد");

    public static LocalText DayTwo { get; } = new("2 days", "يومان");

    public static LocalText DayTwoGenitive { get; } = new("2 days", "يومين");

    public static LocalText DaysFew { get; } = new("{0} days", "{0} أيام");

    public static LocalText DaysMany { get; } = new("{0} days", "{0} يومًا");

    public static LocalText DaysHundred { get; } = new("{0} days", "{0} يوم");

    public static LocalText MonthOne { get; } = new("1 month", "شهر واحد");

    public static LocalText MonthTwo { get; } = new("2 months", "شهران");

    public static LocalText MonthTwoGenitive { get; } = new("2 months", "شهرين");

    public static LocalText MonthsFew { get; } = new("{0} months", "{0} أشهر");

    public static LocalText MonthsMany { get; } = new("{0} months", "{0} شهرًا");

    public static LocalText MonthsHundred { get; } = new("{0} months", "{0} شهر");

    // Check-in: the result card

    public static LocalText HeadlineGranted { get; } = new("Access granted", "مسموح بالدخول");

    public static LocalText HeadlineRenewSoon { get; } = new("Renew soon", "جدّد قريبًا");

    public static LocalText HeadlineDenied { get; } = new("Access denied", "الدخول مرفوض");

    public static LocalText HeadlineUnknown { get; } = new("Not recognised", "رمز غير معروف");

    public static LocalText StatusActiveDays { get; } = new("Active · {0} left", "فعّال · متبقٍ {0}");

    public static LocalText StatusExpiresToday { get; } = new("Expires today", "ينتهي اليوم");

    public static LocalText StatusExpiresTomorrow { get; } = new("Expires tomorrow", "ينتهي غدًا");

    public static LocalText StatusExpiresIn { get; } = new("Expires in {0}", "ينتهي بعد {0}");

    public static LocalText StatusExpiredYesterday { get; } = new("Expired yesterday", "انتهى أمس");

    public static LocalText StatusExpiredAgo { get; } = new("Expired {0} ago", "انتهى منذ {0}");

    public static LocalText StatusNoSubscription { get; } = new("No subscription yet", "لا يوجد اشتراك بعد");

    public static LocalText StatusUnknownCode { get; } = new("Unknown QR code", "رمز QR غير معروف");

    public static LocalText ShortUnknown { get; } = new("Unknown", "غير معروف");

    public static LocalText UnknownMemberName { get; } = new("Unknown member", "مشترك غير معروف");

    public static LocalText ScannedCodeLine { get; } = new("Scanned at {0}", "مُسح في {0}");

    public static LocalText DaysLeftCaption { get; } = new("days left", "الأيام المتبقية");

    public static LocalText DayLeftCaption { get; } = new("day left", "يوم متبقٍ");

    public static LocalText DaysOverdueCaption { get; } = new("days since expiry", "أيام التأخير");

    public static LocalText DayOverdueCaption { get; } = new("day since expiry", "يوم تأخير");

    public static LocalText NoteGranted { get; } = new("Entry logged at {0}.", "سُجّل الدخول الساعة {0}.");

    public static LocalText NoteRenewSoon { get; } = new("Entry logged. Remind the member to renew.", "سُجّل الدخول. ذكّر المشترك بالتجديد.");

    public static LocalText NoteExpired { get; } = new("Not let in. Renew the subscription to let them in.", "لم يُسمح بالدخول. جدّد الاشتراك ليتمكّن من الدخول.");

    public static LocalText NoteNoSubscription { get; } = new(
        "This member has not paid yet. Take the first payment to let them in.",
        "لم يدفع هذا المشترك بعد. استلم الدفعة الأولى ليتمكّن من الدخول.");

    public static LocalText NoteUnknown { get; } = new(
        "No member has this code. An old QR stops working when a new one is issued.",
        "لا يوجد مشترك بهذا الرمز. يتوقف رمز QR القديم عن العمل عند إصدار رمز جديد.");

    public static LocalText Plan { get; } = new("Plan", "الاشتراك");

    public static LocalText Started { get; } = new("Started", "تاريخ البدء");

    public static LocalText ValidUntilCaption { get; } = new("Valid until", "صالح حتى");

    public static LocalText ExpiredOnCaption { get; } = new("Expired on", "انتهى في");

    public static LocalText UsedPercent { get; } = new("{0} of the plan used", "استُهلك {0} من مدة الاشتراك");

    public static LocalText RenewSubscription { get; } = new("Renew subscription", "تجديد الاشتراك");

    public static LocalText WaitingTitle { get; } = new("Ready to scan", "جاهز للمسح");

    public static LocalText WaitingText { get; } = new(
        "The member shows the QR code to the camera, or type the member number below.",
        "يعرض المشترك رمز QR أمام الكاميرا، أو اكتب رقم المشترك في الأسفل.");

    public static LocalText RecentTitle { get; } = new("Recent check-ins", "آخر عمليات الدخول");

    public static LocalText TodayCount { get; } = new("{0} entries today", "عدد الدخول اليوم: {0}");

    // Check-in: typed member number and camera

    public static LocalText ManualLabel { get; } = new("No phone? Member number", "بلا هاتف؟ رقم المشترك");

    public static LocalText ManualPlaceholder { get; } = new("e.g. 00231", "مثال: 00231");

    public static LocalText ManualInvalid { get; } = new("Type the member number, e.g. 231 or #00231.", "اكتب رقم المشترك، مثل 231 أو #00231.");

    public static LocalText CheckInButton { get; } = new("Check in", "تسجيل دخول");

    public static LocalText CameraLabel { get; } = new("Camera", "الكاميرا");

    public static LocalText DefaultCamera { get; } = new("Default camera", "الكاميرا الافتراضية");

    public static LocalText CameraNotSaved { get; } = new("The camera choice could not be saved on this PC.", "تعذّر حفظ اختيار الكاميرا على هذا الجهاز.");

    public static LocalText CameraSample { get; } = new("Camera off · sample data", "الكاميرا متوقفة · بيانات تجريبية");

    public static LocalText CameraStarting { get; } = new("Starting the camera…", "جارٍ تشغيل الكاميرا…");

    public static LocalText CameraScanning { get; } = new("{0} · scanning", "{0} · جارٍ المسح");

    public static LocalText CameraNone { get; } = new("No camera found", "لم يتم العثور على كاميرا");

    public static LocalText CameraFailed { get; } = new("Camera not available", "الكاميرا غير متاحة");

    public static LocalText CameraFailedWithError { get; } = new("Camera not available: {0}", "الكاميرا غير متاحة: {0}");

    public static LocalText CameraOff { get; } = new("Camera off", "الكاميرا متوقفة");

    public static LocalText ScanHintTitle { get; } = new("Show your QR code to the camera", "اعرض رمز QR أمام الكاميرا");

    public static LocalText ScanHintText { get; } = new("Turn your phone brightness up", "ارفع سطوع شاشة هاتفك");

    public static LocalText ScanHintNoCamera { get; } = new("No camera", "لا توجد كاميرا");

    public static LocalText ScanHintNoCameraText { get; } = new(
        "Type the member number below, or use the barcode scanner.",
        "اكتب رقم المشترك في الأسفل، أو استخدم قارئ الباركود.");

    public static LocalText PhotoHint { get; } = new("Fit the face inside the oval", "اجعل الوجه داخل الإطار");

    public static LocalText PhotoHintNoCamera { get; } = new("Connect a camera to take the photo", "صِل كاميرا لالتقاط الصورة");

    // New member

    public static LocalText IdCardTitle { get; } = new("ID card", "البطاقة الشخصية");

    public static LocalText ScanAgain { get; } = new("Scan again", "إعادة المسح");

    public static LocalText ScanPlaceholder { get; } = new("Click here, then scan the ID barcode", "انقر هنا ثم امسح باركود البطاقة");

    public static LocalText ScanHint { get; } = new(
        "Scan the barcode on the back of the ID card into the box above, or type the fields.",
        "امسح الباركود على ظهر البطاقة في الحقل أعلاه، أو اكتب الحقول يدويًا.");

    public static LocalText ScanRead { get; } = new(
        "ID read: {0} of 7 fields filled. Check them before saving.",
        "قُرئت البطاقة وعُبّئ {0} من 7 حقول. تحقّق منها قبل الحفظ.");

    public static LocalText ScanNotRecognised { get; } = new(
        "This barcode was not recognised. Type the fields by hand.",
        "لم يتم التعرف على هذا الباركود. اكتب الحقول يدويًا.");

    public static LocalText FirstName { get; } = new("First name", "الاسم");

    public static LocalText FatherName { get; } = new("Father's name", "اسم الأب");

    public static LocalText LastName { get; } = new("Surname", "النسبة");

    public static LocalText MotherName { get; } = new("Mother's name", "اسم الأم");

    public static LocalText BirthDate { get; } = new("Birth date", "تاريخ الولادة");

    public static LocalText BirthPlace { get; } = new("Birth place", "مكان الولادة");

    public static LocalText NationalNo { get; } = new("National number", "الرقم الوطني");

    public static LocalText DatePlaceholder { get; } = new("e.g. 14/03/1999", "مثال: 14/03/1999");

    public static LocalText PhoneTitle { get; } = new("Phone", "الهاتف");

    public static LocalText MobileNumber { get; } = new("Mobile number", "رقم الموبايل");

    public static LocalText PhonePlaceholder { get; } = new("944 123 456", "944 123 456");

    public static LocalText PhoneHelp { get; } = new("Used to find the member at the door.", "يُستخدم للبحث عن المشترك عند الدخول.");

    public static LocalText PhoneSavedAs { get; } = new("Saved as {0}", "يُحفظ بالشكل {0}");

    public static LocalText PhotoTitle { get; } = new("Photo", "الصورة");

    public static LocalText TakePhoto { get; } = new("Take photo", "التقاط صورة");

    public static LocalText Retake { get; } = new("Retake photo", "إعادة الالتقاط");

    public static LocalText PhotoTaken { get; } = new("Photo taken. It is saved with the member.", "التُقطت الصورة، وتُحفظ مع المشترك.");

    public static LocalText NoCameraPicture { get; } = new("The camera has no picture yet.", "لا توجد صورة من الكاميرا بعد.");

    public static LocalText MemberQrTitle { get; } = new("Member QR", "رمز QR للمشترك");

    public static LocalText QrWait { get; } = new("The QR code is created when you save.", "يُنشأ الرمز عند الحفظ.");

    public static LocalText QrHelp { get; } = new(
        "Show it on screen so the member can photograph it, or copy or save it and send it.",
        "اعرضه على الشاشة ليصوّره المشترك بهاتفه، أو انسخه أو احفظه وأرسله.");

    public static LocalText CopyImage { get; } = new("Copy image", "نسخ الصورة");

    public static LocalText SavePng { get; } = new("Save PNG", "حفظ PNG");

    public static LocalText Print { get; } = new("Print", "طباعة");

    public static LocalText QrCopied { get; } = new("QR image copied. Paste it in WhatsApp.", "نُسخت صورة الرمز. الصقها في واتساب.");

    public static LocalText QrCopyFailed { get; } = new("Could not copy the image. Try again.", "تعذّر نسخ الصورة. حاول مرة أخرى.");

    public static LocalText QrSaved { get; } = new("QR image saved.", "حُفظت صورة الرمز.");

    public static LocalText QrSaveFailed { get; } = new("Could not save the image: {0}", "تعذّر حفظ الصورة: {0}");

    public static LocalText PngFilter { get; } = new("PNG image (*.png)|*.png", "صورة PNG (*.png)|*.png");

    public static LocalText PrintFailed { get; } = new("Could not print: {0}", "تعذّرت الطباعة: {0}");

    public static LocalText QrPrintHelp { get; } = new("Show this code at the gym door.", "اعرض هذا الرمز عند باب النادي.");

    public static LocalText SubscriptionTitle { get; } = new("Subscription", "الاشتراك");

    public static LocalText MemberType { get; } = new("Member type", "نوع المشترك");

    public static LocalText TypeRegular { get; } = new("Regular member", "مشترك عادي");

    public static LocalText TypeTrainer { get; } = new("Private trainer", "مدرب خاص");

    public static LocalText Duration { get; } = new("Duration", "المدة");

    public static LocalText Starts { get; } = new("Starts", "يبدأ");

    public static LocalText Ends { get; } = new("Ends", "ينتهي");

    public static LocalText Payment { get; } = new("Payment", "الدفع");

    public static LocalText Cash { get; } = new("Cash", "نقدًا");

    public static LocalText Total { get; } = new("Total", "المجموع");

    public static LocalText TodayNote { get; } = new("Today", "اليوم");

    public static LocalText PayServiceNow { get; } = new("Pay the private training service now", "دفع خدمة التدريب الخاص الآن");

    public static LocalText ServiceDetail { get; } = new("{0} × {1} a month", "{0} × {1} شهريًا");

    public static LocalText SaveMember { get; } = new("Save member", "حفظ المشترك");

    public static LocalText Saving { get; } = new("Saving…", "جارٍ الحفظ…");

    public static LocalText Required { get; } = new("Required", "مطلوب");

    public static LocalText DateInvalid { get; } = new("Use day/month/year, e.g. 14/03/1999.", "استخدم الصيغة يوم/شهر/سنة، مثل 14/03/1999.");

    public static LocalText FixFields { get; } = new("Fill in the fields marked in red.", "أكمل الحقول المحددة باللون الأحمر.");

    public static LocalText NoPhotoTitle { get; } = new("No photo", "لا توجد صورة");

    public static LocalText NoPhotoText { get; } = new(
        "Save the member without a photo? You can add it later on the Members page.",
        "حفظ المشترك بدون صورة؟ يمكنك إضافتها لاحقًا من صفحة المشتركين.");

    public static LocalText SaveWithoutPhoto { get; } = new("Save without photo", "حفظ بدون صورة");

    public static LocalText PhotoUploadFailed { get; } = new("The member is saved, but the photo is not: {0}", "حُفظ المشترك لكن لم تُحفظ الصورة: {0}");

    public static LocalText ServicePaymentFailed { get; } = new(
        "The member is saved, but the private training payment is not: {0}",
        "حُفظ المشترك لكن لم تُسجَّل دفعة التدريب الخاص: {0}");

    public static LocalText MemberSaved { get; } = new("Member saved · {0}", "تم حفظ المشترك · {0}");

    public static LocalText RegisterAnother { get; } = new("Register another member", "تسجيل مشترك آخر");

    // Members

    public static LocalText SearchMembersPlaceholder { get; } = new(
        "Name, phone, national no., member no. or QR",
        "الاسم أو الهاتف أو الرقم الوطني أو رقم المشترك أو QR");

    public static LocalText ScanQrToSearch { get; } = new("Scan a QR code with the camera", "امسح رمز QR بالكاميرا");

    public static LocalText FilterAll { get; } = new("All", "الكل");

    public static LocalText MembersFound { get; } = new("{0} members", "عدد المشتركين: {0}");

    public static LocalText MembersFoundOne { get; } = new("1 member", "مشترك واحد");

    public static LocalText MembersShown { get; } = new("Showing {0} of {1} members", "عرض {0} من أصل {1}");

    public static LocalText NoMemberTitle { get; } = new("Pick a member", "اختر مشتركًا");

    public static LocalText NoMemberText { get; } = new("Search, then pick a member to see the details.", "ابحث ثم اختر مشتركًا لعرض تفاصيله.");

    public static LocalText Renew { get; } = new("Renew", "تجديد");

    public static LocalText FirstPayment { get; } = new("Take payment", "استلام الدفعة");

    public static LocalText ShowQr { get; } = new("Show QR", "عرض الرمز");

    public static LocalText EditPhone { get; } = new("Change phone", "تغيير الهاتف");

    public static LocalText ChangePhoto { get; } = new("Change photo", "تغيير الصورة");

    public static LocalText MemberSince { get; } = new("Member since", "مشترك منذ");

    public static LocalText PrivateTrainingFact { get; } = new("Private training", "التدريب الخاص");

    public static LocalText TrainerFact { get; } = new("Private trainer", "المدرب الخاص");

    public static LocalText NoTrainer { get; } = new("None", "لا يوجد");

    public static LocalText PaymentsTitle { get; } = new("Payments", "الدفعات");

    public static LocalText CheckInsTitle { get; } = new("Check-ins", "سجل الدخول");

    public static LocalText ColumnWhat { get; } = new("Payment", "الدفعة");

    public static LocalText ColumnPeriod { get; } = new("Period", "الفترة");

    public static LocalText ColumnPaid { get; } = new("Paid", "المبلغ");

    public static LocalText ColumnWhen { get; } = new("Date and time", "التاريخ والوقت");

    public static LocalText ColumnResult { get; } = new("Result", "النتيجة");

    public static LocalText KindMembership { get; } = new("Membership", "اشتراك");

    public static LocalText KindPrivate { get; } = new("Private training", "تدريب خاص");

    public static LocalText PeriodFrom { get; } = new("From {0}", "من {0}");

    public static LocalText PeriodTo { get; } = new("To {0}", "إلى {0}");

    public static LocalText ValidUntil { get; } = new("Valid until {0}", "صالح حتى {0}");

    public static LocalText ExpiredOn { get; } = new("Expired on {0}", "انتهى في {0}");

    // Renew

    public static LocalText RenewTitle { get; } = new("Renew subscription", "تجديد الاشتراك");

    public static LocalText RenewConfirm { get; } = new("Renew and take payment", "تجديد واستلام المبلغ");

    public static LocalText RenewedUntil { get; } = new("Renewed. Valid until {0}.", "تم التجديد. صالح حتى {0}.");

    public static LocalText StartsAfterCurrent { get; } = new("The day after the current end", "في اليوم التالي لانتهاء الاشتراك الحالي");

    public static LocalText StartsToday { get; } = new("Today", "اليوم");

    public static LocalText ChooseDuration { get; } = new("Choose a duration.", "اختر المدة.");

    public static LocalText ChooseMember { get; } = new("Choose a member.", "اختر مشتركًا.");

    // QR, phone and photo dialogs

    public static LocalText ReissueQr { get; } = new("Issue a new QR", "إصدار رمز جديد");

    public static LocalText ReissueQrTitle { get; } = new("Issue a new QR code?", "إصدار رمز QR جديد؟");

    public static LocalText ReissueQrText { get; } = new(
        "The old QR code stops working at once. Use this when a member lost the phone or shared the code.",
        "يتوقف الرمز القديم عن العمل فورًا. استخدم هذا إذا فقد المشترك هاتفه أو شارك رمزه.");

    public static LocalText QrReissued { get; } = new("New QR code issued. The old one no longer works.", "أُصدر رمز جديد. الرمز القديم لم يعد يعمل.");

    public static LocalText EditPhoneTitle { get; } = new("Change phone number", "تغيير رقم الهاتف");

    public static LocalText PhoneSaved { get; } = new("Phone number saved.", "تم حفظ رقم الهاتف.");

    public static LocalText ChangePhotoTitle { get; } = new("New member photo", "صورة جديدة للمشترك");

    public static LocalText SavePhoto { get; } = new("Save photo", "حفظ الصورة");

    public static LocalText PhotoSaved { get; } = new("Photo saved.", "تم حفظ الصورة.");

    public static LocalText TakePhotoFirst { get; } = new("Take a photo first.", "التقط صورة أولًا.");

    // Private trainers

    public static LocalText AddTrainer { get; } = new("Add a private trainer", "إضافة مدرب خاص");

    public static LocalText AddTrainerTitle { get; } = new("Make a member a private trainer", "تحويل مشترك إلى مدرب خاص");

    public static LocalText MakeTrainer { get; } = new("Make private trainer", "تحويل إلى مدرب خاص");

    public static LocalText AlreadyTrainer { get; } = new("Already a private trainer", "مدرب خاص مسبقًا");

    public static LocalText TrainerAdded { get; } = new("The member is now a private trainer.", "أصبح المشترك مدربًا خاصًا.");

    public static LocalText MakeRegular { get; } = new("Change to regular member", "تحويل إلى مشترك عادي");

    public static LocalText MakeRegularTitle { get; } = new("Change to a regular member?", "تحويل إلى مشترك عادي؟");

    public static LocalText MakeRegularText { get; } = new(
        "He stays a member with his own subscription, but can no longer have clients.",
        "يبقى مشتركًا باشتراكه، لكن لا يمكن ربط عملاء به بعد الآن.");

    public static LocalText TrainerRemoved { get; } = new("Changed to a regular member.", "تم التحويل إلى مشترك عادي.");

    public static LocalText UnlinkFirstTitle { get; } = new("Unlink the clients first", "افصل العملاء أولًا");

    public static LocalText PayPrivate { get; } = new("Pay private training", "دفع التدريب الخاص");

    public static LocalText RenewPrivate { get; } = new("Renew private training", "تجديد التدريب الخاص");

    public static LocalText PayPrivateTitle { get; } = new("Private training service", "خدمة التدريب الخاص");

    public static LocalText PayPrivateText { get; } = new(
        "The price is the months times the monthly fee in the settings.",
        "السعر = عدد الأشهر × الرسم الشهري المحدد في الإعدادات.");

    public static LocalText PayConfirm { get; } = new("Take payment", "استلام المبلغ");

    public static LocalText PrivateServiceDuration { get; } = new("Months of private training", "أشهر التدريب الخاص");

    public static LocalText PrivatePaidUntil { get; } = new("Private training paid until {0}.", "تم دفع التدريب الخاص حتى {0}.");

    public static LocalText ServicePaidUntil { get; } = new("Private training paid until {0}", "التدريب الخاص مدفوع حتى {0}");

    public static LocalText ServiceEndedOn { get; } = new("Private training ended on {0}", "انتهى التدريب الخاص في {0}");

    public static LocalText ServiceNotPaid { get; } = new("Private training not paid", "التدريب الخاص غير مدفوع");

    public static LocalText ServiceShortPaid { get; } = new("Paid until {0}", "مدفوع حتى {0}");

    public static LocalText ServiceShortEnded { get; } = new("Ended on {0}", "انتهى في {0}");

    public static LocalText ServiceShortNone { get; } = new("Not paid", "غير مدفوع");

    public static LocalText MonthlyFee { get; } = new("Monthly private training fee: {0}", "رسم التدريب الخاص الشهري: {0}");

    public static LocalText ClientsTitle { get; } = new("Linked clients", "العملاء المرتبطون");

    public static LocalText ClientCount { get; } = new("Clients: {0}", "العملاء: {0}");

    public static LocalText TrainerLine { get; } = new("{0} · {1}", "{0} · {1}");

    public static LocalText LinkClient { get; } = new("Link a client", "ربط عميل");

    public static LocalText LinkClientTitle { get; } = new("Link a client to this trainer", "ربط عميل بهذا المدرب");

    public static LocalText TrainsWith { get; } = new("Trains with {0} now", "يتدرب حاليًا مع {0}");

    public static LocalText AlreadyClient { get; } = new("Already a client of this trainer", "عميل لهذا المدرب مسبقًا");

    public static LocalText ClientLinked { get; } = new("Client linked.", "تم ربط العميل.");

    public static LocalText Unlink { get; } = new("Unlink", "فصل");

    public static LocalText UnlinkTitle { get; } = new("Unlink this client?", "فصل هذا العميل؟");

    public static LocalText UnlinkText { get; } = new(
        "The member keeps his own subscription; only the link to the trainer is removed.",
        "يحتفظ المشترك باشتراكه، ويُزال الربط بالمدرب فقط.");

    public static LocalText ClientUnlinked { get; } = new("Client unlinked.", "تم فصل العميل.");

    public static LocalText NoClientsTitle { get; } = new("No clients yet", "لا يوجد عملاء بعد");

    public static LocalText NoClientsText { get; } = new("Link the members this trainer trains.", "اربط المشتركين الذين يدرّبهم هذا المدرب.");

    public static LocalText NoTrainersTitle { get; } = new("No private trainers", "لا يوجد مدربون خاصون");

    public static LocalText NoTrainersText { get; } = new("Make a member a private trainer to start.", "حوّل مشتركًا إلى مدرب خاص للبدء.");

    // Errors the server can give on these screens

    public static LocalText ErrorTrainerHasClients { get; } = new(
        "This private trainer still has linked clients. Unlink them first, then change him to a regular member.",
        "ما زال لهذا المدرب الخاص عملاء مرتبطون به. افصلهم أولًا ثم حوّله إلى مشترك عادي.");

    public static LocalText ErrorCannotLinkSelf { get; } = new("A private trainer cannot be his own client.", "لا يمكن أن يكون المدرب الخاص عميلًا لنفسه.");

    public static LocalText ErrorClientNotLinked { get; } = new("This member is not a client of this trainer any more.", "هذا المشترك لم يعد عميلًا لهذا المدرب.");

    public static LocalText ErrorPhotoTooLarge { get; } = new("The photo is too large (5 MB at most).", "الصورة كبيرة جدًا (5 ميغابايت كحد أقصى).");

    public static LocalText ErrorPhotoInvalid { get; } = new("This photo cannot be used. Take it again.", "لا يمكن استخدام هذه الصورة. التقطها من جديد.");

    public static LocalText ErrorPlanGone { get; } = new("This plan is no longer offered. Pick another one.", "لم يعد هذا الاشتراك متاحًا. اختر غيره.");

    public static LocalText ErrorMemberGone { get; } = new("This member no longer exists.", "هذا المشترك لم يعد موجودًا.");
}
