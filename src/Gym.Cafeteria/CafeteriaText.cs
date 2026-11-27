using Gym.UI;

namespace Gym.Cafeteria;

/// <summary>
/// Every string of the Cafeteria app, in English and Arabic (docs/PLAN.md, "How to add strings").
/// XAML: <c>{ui:Loc {x:Static local:CafeteriaText.Key}}</c>. Code: <c>CafeteriaText.Key.For(UiLanguage.Current)</c>.
/// </summary>
public static class CafeteriaText
{
    // App and sidebar

    public static LocalText AppName { get; } = new("Cafeteria", "الكافتيريا");

    public static LocalText NavLabel { get; } = new("Cafeteria menu", "قائمة الكافتيريا");

    public static LocalText Cashier { get; } = new("Sell", "البيع");

    public static LocalText CashierTitle { get; } = new("Cashier", "نقطة البيع");

    public static LocalText Stock { get; } = new("Stock", "المخزون");

    public static LocalText ShiftClose { get; } = new("Shift close", "إغلاق الوردية");

    public static LocalText Receipts { get; } = new("Receipts", "الإيصالات");

    public static LocalText ReceiptsTitle { get; } = new("Receipts and printer", "الإيصالات والطابعة");

    // Cashier: items

    public static LocalText SearchLabel { get; } = new("Scan a barcode or search", "امسح الباركود أو ابحث");

    public static LocalText SearchPlaceholder { get; } = new("Scan an item barcode, or type a name", "امسح باركود الصنف أو اكتب اسمه");

    public static LocalText AllCategories { get; } = new("All", "الكل");

    public static LocalText NoItemsFound { get; } = new("No item matches this search.", "لا يوجد صنف يطابق هذا البحث.");

    public static LocalText StockLeft { get; } = new("{0} left", "المتبقي: {0}");

    public static LocalText LowStockLeft { get; } = new("Low stock: {0} left", "مخزون منخفض: {0}");

    public static LocalText OutOfStock { get; } = new("Out of stock", "نفد من المخزون");

    /// <summary>The short status pill in the stock list.</summary>
    public static LocalText OutShort { get; } = new("Out of stock", "نافد");

    public static LocalText MinStockShort { get; } = new("Minimum", "الحد الأدنى");

    public static LocalText AddItem { get; } = new("Add {0}, {1}", "إضافة {0}، {1}");

    public static LocalText OneItemLow { get; } = new("1 item low on stock", "صنف واحد ناقص في المخزون");

    public static LocalText ItemsLow { get; } = new("{0} items low on stock", "أصناف ناقصة في المخزون: {0}");

    public static LocalText MoreItems { get; } = new("+{0} more", "أصناف أخرى: {0}");

    // Cashier: the order

    public static LocalText CurrentOrder { get; } = new("Current order", "الطلب الحالي");

    public static LocalText OneItem { get; } = new("1 item", "قطعة واحدة");

    public static LocalText ItemCount { get; } = new("{0} items", "عدد القطع: {0}");

    public static LocalText Each { get; } = new("each", "للقطعة");

    public static LocalText OnlyInStock { get; } = new("Only {0} in stock", "المتوفر في المخزون: {0} فقط");

    public static LocalText RemoveOne { get; } = new("Remove one {0}", "إنقاص واحدة من {0}");

    public static LocalText RemoveItem { get; } = new("Remove {0}", "إزالة {0}");

    public static LocalText AddOne { get; } = new("Add one {0}", "إضافة واحدة من {0}");

    public static LocalText EmptyCart { get; } = new("Tap an item or scan a barcode to add it.", "اضغط على صنف أو امسح الباركود لإضافته.");

    public static LocalText CashReceived { get; } = new("Cash received", "المبلغ المستلم");

    public static LocalText CashPlaceholder { get; } = new("Optional: type it to see the change", "اختياري: اكتبه لمعرفة الباقي");

    public static LocalText Change { get; } = new("Change", "الباقي");

    public static LocalText ChangeToGive { get; } = new("Change to give back", "الباقي للزبون");

    public static LocalText CashTooLow { get; } = new("Less than the total", "أقل من المجموع");

    public static LocalText InvalidAmount { get; } = new("Enter an amount in SYP", "أدخل مبلغًا بالليرة السورية");

    public static LocalText TotalCash { get; } = new("Total · cash", "المجموع · نقدًا");

    public static LocalText ChargeAmount { get; } = new("Charge {0}", "تحصيل {0}");

    public static LocalText Charging { get; } = new("Saving the sale…", "جارٍ حفظ البيع…");

    public static LocalText PaidInCash { get; } = new("Paid in cash", "تم الدفع نقدًا");

    public static LocalText PaidDetail { get; } = new("Receipt {0} · {1}", "إيصال رقم {0} · {1}");

    public static LocalText Printing { get; } = new("Printing the receipt…", "جارٍ طباعة الإيصال…");

    public static LocalText ReceiptPrinted { get; } = new("Receipt printed.", "تمت طباعة الإيصال.");

    public static LocalText SampleNotPrinted { get; } = new("Sample data: no receipt is printed.", "بيانات تجريبية: لا تتم طباعة الإيصال.");

    public static LocalText PrintFailedDetail { get; } = new("Could not print: {0}", "تعذّرت الطباعة: {0}");

    public static LocalText ReprintReceipt { get; } = new("Print the receipt again", "إعادة طباعة الإيصال");

    public static LocalText NewOrder { get; } = new("New order", "طلب جديد");

    public static LocalText ClearOrderTitle { get; } = new("Clear the order?", "مسح الطلب؟");

    public static LocalText ClearOrderText { get; } = new("All items are taken out of the order.", "ستتم إزالة جميع الأصناف من الطلب.");

    // Cashier: messages

    public static LocalText ItemOutOfStock { get; } = new("{0} is out of stock.", "نفد {0} من المخزون.");

    public static LocalText NoMoreInStock { get; } = new("Only {1} of {0} in stock.", "المتوفر من {0} في المخزون: {1} فقط.");

    public static LocalText ItemNotForSale { get; } = new("{0} is not for sale.", "{0} غير متاح للبيع.");

    public static LocalText UnknownBarcode { get; } = new("No item has the barcode {0}.", "لا يوجد صنف بالباركود {0}.");

    public static LocalText NotEnoughStockTitle { get; } = new("Not enough stock", "الكمية غير كافية");

    public static LocalText NotEnoughStockText { get; } = new(
        "Another till may have sold these items. Lower the quantities in the order, then charge again.",
        "ربما باعت محطة أخرى هذه الأصناف. خفّض الكميات في الطلب ثم أعد التحصيل.");

    public static LocalText ShortageDetail { get; } = new("{0} in the order, {1} in stock", "في الطلب {0}، في المخزون {1}");

    public static LocalText ChargeTimeoutTitle { get; } = new("No answer from the server", "لا يوجد رد من الخادم");

    public static LocalText ChargeTimeoutText { get; } = new(
        "The sale may or may not be saved. Check the Receipts screen before you charge again.",
        "قد يكون البيع محفوظًا أو لا. تحقّق من شاشة الإيصالات قبل إعادة التحصيل.");

    // Shift (cashier screen and shift close)

    public static LocalText ShiftOpenSince { get; } = new("Shift open since {0}", "الوردية مفتوحة منذ {0}");

    public static LocalText OneSale { get; } = new("1 sale", "عملية بيع واحدة");

    public static LocalText SaleCount { get; } = new("{0} sales", "عدد المبيعات: {0}");

    public static LocalText NoOpenShift { get; } = new("No open shift", "لا توجد وردية مفتوحة");

    public static LocalText FirstSaleOpensShift { get; } = new("The first sale opens a shift by itself.", "أول عملية بيع تفتح وردية تلقائيًا.");

    public static LocalText ShiftReport { get; } = new("Shift report", "تقرير الوردية");

    public static LocalText Print { get; } = new("Print", "طباعة");

    public static LocalText ShiftTitle { get; } = new("Shift {0}", "الوردية رقم {0}");

    public static LocalText ShiftClosedState { get; } = new("Closed", "مغلقة");

    public static LocalText CashShort { get; } = new("Cash short", "نقص في النقد");

    public static LocalText OpenedAt { get; } = new("Opened {0}", "بدأت {0}");

    public static LocalText OpenedClosed { get; } = new("Opened {0} · closed {1}", "بدأت {0} · انتهت {1}");

    public static LocalText CountedWas { get; } = new("Counted cash: {0}", "النقد المعدود: {0}");

    public static LocalText Quantity { get; } = new("Quantity", "الكمية");

    public static LocalText NoShiftsYet { get; } = new("No shifts yet", "لا توجد ورديات بعد");

    public static LocalText CloseShiftTitle { get; } = new("Close the shift", "إغلاق الوردية");

    public static LocalText CountedCashHint { get; } = new("Count the cash drawer, or leave it empty.", "عُدّ النقود في الدرج، أو اترك الحقل فارغًا.");

    public static LocalText CashMissing { get; } = new("Cash missing", "نقص في النقد");

    public static LocalText CashExtra { get; } = new("Extra cash", "زيادة في النقد");

    public static LocalText CashMatches { get; } = new("The cash matches", "النقد مطابق");

    public static LocalText CloseShift { get; } = new("Close shift", "إغلاق الوردية");

    public static LocalText OpenShift { get; } = new("Open shift", "فتح وردية");

    public static LocalText PastShifts { get; } = new("Past shifts · last 14 days", "الورديات السابقة · آخر 14 يومًا");

    public static LocalText CloseShiftConfirm { get; } = new("{0} sales, {1}. Close the shift now?", "عدد المبيعات {0}، المجموع {1}. هل تريد إغلاق الوردية الآن؟");

    public static LocalText CloseShiftConfirmCounted { get; } = new(
        "{0} sales, {1}. Counted cash: {2}. Close the shift now?",
        "عدد المبيعات {0}، المجموع {1}، النقد المعدود {2}. هل تريد إغلاق الوردية الآن؟");

    public static LocalText ShiftClosedToast { get; } = new("Shift closed.", "تم إغلاق الوردية.");

    public static LocalText ShiftOpenedToast { get; } = new("Shift opened.", "تم فتح الوردية.");

    public static LocalText ZReportPrinted { get; } = new("Shift report printed.", "تمت طباعة تقرير الوردية.");

    // Receipt and shift report (printed)

    public static LocalText ReceiptTitle { get; } = new("Cafeteria receipt", "إيصال الكافتيريا");

    public static LocalText ReceiptNumber { get; } = new("Receipt no.", "رقم الإيصال");

    public static LocalText ReceiptDate { get; } = new("Date", "التاريخ");

    public static LocalText ReceiptStation { get; } = new("Station", "المحطة");

    public static LocalText ReceiptCashier { get; } = new("Cashier", "الكاشير");

    public static LocalText Total { get; } = new("Total", "المجموع");

    public static LocalText ReceiptPayment { get; } = new("Payment", "الدفع");

    public static LocalText Cash { get; } = new("Cash", "نقدًا");

    public static LocalText ReceiptThanks { get; } = new("Thank you for your visit", "شكرًا لزيارتكم");

    public static LocalText ZReportTitle { get; } = new("Shift report (Z)", "تقرير إغلاق الوردية");

    public static LocalText ShiftNumber { get; } = new("Shift no.", "رقم الوردية");

    public static LocalText ShiftOpened { get; } = new("Opened", "بدأت");

    public static LocalText ShiftClosed { get; } = new("Closed", "انتهت");

    public static LocalText ShiftStillOpen { get; } = new("Open", "مفتوحة");

    public static LocalText SalesCount { get; } = new("Sales", "عدد المبيعات");

    public static LocalText SalesTotal { get; } = new("Sales total", "مجموع المبيعات");

    public static LocalText OpeningCash { get; } = new("Opening cash", "نقد البداية");

    public static LocalText ExpectedCash { get; } = new("Expected cash", "النقد المتوقع");

    public static LocalText CountedCash { get; } = new("Counted cash", "النقد المعدود");

    public static LocalText NotCounted { get; } = new("Not counted", "لم يُعَدّ");

    public static LocalText CashDifference { get; } = new("Difference", "الفرق");

    public static LocalText ItemsSold { get; } = new("Items sold", "الأصناف المباعة");

    public static LocalText NoSalesYet { get; } = new("No sales yet.", "لا توجد مبيعات بعد.");

    public static LocalText PiecesSold { get; } = new("Qty {0}", "الكمية: {0}");

    public static LocalText ShiftNote { get; } = new("Note", "ملاحظة");

    public static LocalText PrintedAt { get; } = new("Printed {0}", "طُبع في {0}");

    public static LocalText TestPageTitle { get; } = new("Printer test", "اختبار الطابعة");

    public static LocalText TestPageSample { get; } = new("Protein bar", "لوح بروتين");

    public static LocalText TestPageFooter { get; } = new("If both languages read well, the printer is ready.", "إذا ظهرت اللغتان بوضوح فالطابعة جاهزة.");

    // Printer

    public static LocalText Printer { get; } = new("Printer", "الطابعة");

    public static LocalText PrintFailed { get; } = new("The printer did not accept the receipt.", "لم تقبل الطابعة الإيصال.");

    public static LocalText NoDefaultPrinter { get; } = new(
        "Windows has no default printer. Pick the receipt printer on the Receipts screen.",
        "لا توجد طابعة افتراضية في ويندوز. اختر طابعة الإيصالات من شاشة الإيصالات.");

    public static LocalText PrinterNotFound { get; } = new("The receipt printer is not installed on this PC:", "طابعة الإيصالات غير مثبتة على هذا الجهاز:");

    public static LocalText ReceiptPrinter { get; } = new("Receipt printer", "طابعة الإيصالات");

    public static LocalText PrinterHint { get; } = new("Saved on this PC.", "يُحفظ على هذا الجهاز.");

    public static LocalText DefaultPrinter { get; } = new("Windows default printer", "طابعة ويندوز الافتراضية");

    public static LocalText TestPrint { get; } = new("Test print", "طباعة تجريبية");

    public static LocalText PrinterNote { get; } = new(
        "Any Windows printer works for now; the receipt is laid out for 80 mm paper.",
        "تعمل أي طابعة ويندوز حاليًا، والإيصال مُعدّ لورق 80 مم.");

    public static LocalText SamplePrinterNote { get; } = new(
        "Sample data: printers are not listed and nothing is printed.",
        "بيانات تجريبية: لا تُعرض الطابعات ولا تتم الطباعة.");

    public static LocalText SavedPrinterMissing { get; } = new(
        "The saved printer is not installed on this PC. Pick another one.",
        "الطابعة المحفوظة غير مثبتة على هذا الجهاز. اختر طابعة أخرى.");

    public static LocalText PrinterSaved { get; } = new("Receipt printer saved.", "تم حفظ طابعة الإيصالات.");

    public static LocalText PrinterSaveFailed { get; } = new("Could not save the printer setting on this PC.", "تعذّر حفظ إعداد الطابعة على هذا الجهاز.");

    // Receipts screen

    public static LocalText SalesOfTheDay { get; } = new("Sales of the day", "مبيعات اليوم");

    public static LocalText OneSaleTotal { get; } = new("1 sale · {0}", "عملية بيع واحدة · {0}");

    public static LocalText SalesTotalLine { get; } = new("{0} sales · {1}", "عدد المبيعات {0} · {1}");

    public static LocalText NoReceipts { get; } = new("No receipts", "لا توجد إيصالات");

    public static LocalText NoReceiptsText { get; } = new("No sales on this day.", "لا توجد مبيعات في هذا اليوم.");

    public static LocalText ReceiptNumbered { get; } = new("Receipt {0}", "إيصال رقم {0}");

    public static LocalText Reprint { get; } = new("Reprint", "إعادة الطباعة");

    public static LocalText ReceiptPreview { get; } = new("Receipt", "الإيصال");

    public static LocalText ReceiptPreviewText { get; } = new("As it prints on 80 mm paper", "كما يُطبع على ورق 80 مم");

    public static LocalText ScannerTest { get; } = new("Barcode scanner test", "اختبار قارئ الباركود");

    public static LocalText ScannerTestText { get; } = new(
        "Tap the field and scan any item. The code and the item it finds show below.",
        "اضغط على الحقل وامسح أي صنف. يظهر الرمز والصنف المطابق في الأسفل.");

    public static LocalText ScanHere { get; } = new("Scan here", "امسح هنا");

    public static LocalText ScanFound { get; } = new("Read {0}: {1}", "تمت قراءة {0}: {1}");

    public static LocalText ScanUnknown { get; } = new("Read {0}: no item has this barcode.", "تمت قراءة {0}: لا يوجد صنف بهذا الباركود.");

    // Stock screen

    public static LocalText StockSearchPlaceholder { get; } = new("Search by name or barcode", "ابحث بالاسم أو الباركود");

    public static LocalText LowOnly { get; } = new("Low stock only", "المخزون المنخفض فقط");

    public static LocalText ShowInactive { get; } = new("Show inactive", "إظهار غير الفعّالة");

    public static LocalText NewItem { get; } = new("New item", "صنف جديد");

    public static LocalText Item { get; } = new("Item", "الصنف");

    public static LocalText Barcode { get; } = new("Barcode", "الباركود");

    public static LocalText Price { get; } = new("Price", "السعر");

    public static LocalText StockColumn { get; } = new("Stock", "المخزون");

    public static LocalText Status { get; } = new("Status", "الحالة");

    public static LocalText MinShort { get; } = new("min {0}", "الحد الأدنى {0}");

    public static LocalText InStock { get; } = new("In stock", "متوفر");

    public static LocalText LowStock { get; } = new("Low stock", "مخزون منخفض");

    public static LocalText Inactive { get; } = new("Inactive", "غير فعّال");

    public static LocalText ItemsShown { get; } = new("{0} items", "عدد الأصناف: {0}");

    public static LocalText InStockNow { get; } = new("In stock now", "المخزون الحالي");

    public static LocalText MinStock { get; } = new("Minimum stock", "الحد الأدنى");

    public static LocalText StockIn { get; } = new("Stock in", "توريد");

    public static LocalText Adjust { get; } = new("Adjust", "تعديل الكمية");

    public static LocalText EditItem { get; } = new("Edit item", "تعديل الصنف");

    public static LocalText History { get; } = new("Stock history", "حركة المخزون");

    public static LocalText NoHistory { get; } = new("No stock changes in the last 90 days.", "لا توجد حركة مخزون خلال آخر 90 يومًا.");

    public static LocalText PickAnItem { get; } = new("Pick an item", "اختر صنفًا");

    public static LocalText PickAnItemText { get; } = new("Its stock, history and actions show here.", "يظهر هنا مخزونه وحركته والإجراءات.");

    public static LocalText MovementStockIn { get; } = new("Stock-in", "توريد");

    public static LocalText MovementSale { get; } = new("Sale", "بيع");

    public static LocalText MovementAdjustment { get; } = new("Adjustment", "تعديل");

    // Stock forms

    public static LocalText StockInTitle { get; } = new("Stock in", "توريد بضاعة");

    public static LocalText SaveStockIn { get; } = new("Save stock-in", "حفظ التوريد");

    public static LocalText QuantityIn { get; } = new("Quantity received", "الكمية المستلمة");

    public static LocalText Note { get; } = new("Note", "ملاحظة");

    public static LocalText StockInNoteHint { get; } = new("Optional, e.g. the supplier or the invoice number", "اختياري، مثل المورد أو رقم الفاتورة");

    public static LocalText EnterQuantity { get; } = new("Enter a whole number from 1 to {0}.", "أدخل عددًا صحيحًا من 1 إلى {0}.");

    public static LocalText StockInSaved { get; } = new("{0}: stock is now {1}.", "{0}: المخزون الآن {1}.");

    public static LocalText AdjustTitle { get; } = new("Adjust stock", "تعديل المخزون");

    public static LocalText SaveAdjustment { get; } = new("Save adjustment", "حفظ التعديل");

    public static LocalText AdjustByChange { get; } = new("Change by", "تغيير بمقدار");

    public static LocalText AdjustByCount { get; } = new("Counted quantity", "الكمية المعدودة");

    public static LocalText ChangeBy { get; } = new("Change", "مقدار التغيير");

    public static LocalText ChangeHint { get; } = new(
        "A negative number lowers the stock (broken, expired), a positive one raises it.",
        "رقم سالب للإنقاص (تالف أو منتهي الصلاحية)، ورقم موجب للزيادة.");

    public static LocalText CountedQuantity { get; } = new("Counted quantity", "الكمية المعدودة");

    public static LocalText CountedHint { get; } = new("The stock becomes this number.", "يصبح المخزون هذا العدد.");

    public static LocalText Reason { get; } = new("Reason", "السبب");

    public static LocalText ReasonHint { get; } = new("e.g. broken, expired, stocktake", "مثل: تالف، منتهي الصلاحية، جرد");

    public static LocalText ReasonRequired { get; } = new("Give a reason.", "اكتب السبب.");

    public static LocalText SampleAdjustReason { get; } = new("Two bottles broken", "عبوتان تالفتان");

    public static LocalText EnterChange { get; } = new("Enter a whole number that is not zero.", "أدخل عددًا صحيحًا غير الصفر.");

    public static LocalText EnterCount { get; } = new("Enter a whole number, 0 or more.", "أدخل عددًا صحيحًا، صفرًا أو أكثر.");

    public static LocalText NewStock { get; } = new("Stock after saving", "المخزون بعد الحفظ");

    public static LocalText StockBelowZero { get; } = new("The stock cannot go below zero (in stock {0}).", "لا يمكن أن يصبح المخزون أقل من الصفر (المتوفر {0}).");

    public static LocalText AdjustmentSaved { get; } = new("{0}: stock is now {1}.", "{0}: المخزون الآن {1}.");

    public static LocalText NewItemTitle { get; } = new("New item", "صنف جديد");

    public static LocalText NameEnglish { get; } = new("Name in English", "الاسم بالإنكليزية");

    public static LocalText NameArabic { get; } = new("Name in Arabic", "الاسم بالعربية");

    public static LocalText Category { get; } = new("Category", "الفئة");

    public static LocalText PriceSyp { get; } = new("Price (SYP)", "السعر بالليرة السورية");

    public static LocalText BarcodeHint { get; } = new("Scan the item into this field, or type it.", "امسح الصنف داخل هذا الحقل أو اكتب الرقم.");

    public static LocalText MinStockHint { get; } = new("Below this the item shows as low.", "تحت هذا العدد يظهر الصنف كناقص.");

    public static LocalText ActiveForSale { get; } = new("Active (for sale)", "فعّال ومتاح للبيع");

    public static LocalText NameRequired { get; } = new("Enter a name in English or Arabic.", "أدخل اسمًا بالإنكليزية أو بالعربية.");

    public static LocalText CategoryRequired { get; } = new("Pick a category.", "اختر فئة.");

    public static LocalText EnterPrice { get; } = new("Enter the price in SYP, 0 or more.", "أدخل السعر بالليرة السورية، صفرًا أو أكثر.");

    public static LocalText ItemSaved { get; } = new("{0} saved.", "تم حفظ {0}.");
}
