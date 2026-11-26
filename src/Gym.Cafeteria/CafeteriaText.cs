using Gym.UI;

namespace Gym.Cafeteria;

/// <summary>
/// Every string of the Cafeteria app, in English and Arabic (docs/PLAN.md, "How to add strings").
/// XAML: <c>{ui:Loc {x:Static local:CafeteriaText.Key}}</c>. Code: <c>CafeteriaText.Key.For(UiLanguage.Current)</c>.
/// </summary>
public static class CafeteriaText
{
    public static LocalText AppName { get; } = new("Cafeteria", "الكافتيريا");

    public static LocalText NavLabel { get; } = new("Cafeteria menu", "قائمة الكافتيريا");

    public static LocalText Cashier { get; } = new("Sell", "البيع");

    public static LocalText CashierTitle { get; } = new("Cashier", "نقطة البيع");

    public static LocalText CashierPlanned { get; } = new("Product grid by category, barcode search, cart with + and −, Charge, then the receipt.", "شبكة الأصناف حسب الفئة، البحث بالباركود، سلة مع + و −، الدفع ثم الإيصال.");

    public static LocalText CashierSample { get; } = new("Sample data: {0} products in {1} categories", "بيانات تجريبية: {0} صنفًا في {1} فئات");

    public static LocalText Stock { get; } = new("Stock", "المخزون");

    public static LocalText StockPlanned { get; } = new("Stock levels, stock-in, adjustments and low-stock warnings.", "مستويات المخزون والتوريد والتعديلات وتنبيهات نقص المخزون.");

    public static LocalText StockSample { get; } = new("Sample data: {0} items low on stock", "بيانات تجريبية: {0} أصناف ناقصة في المخزون");

    public static LocalText ShiftClose { get; } = new("Shift close", "إغلاق الوردية");

    public static LocalText ShiftClosePlanned { get; } = new("The sales summary of the shift, the cash count and closing the shift.", "ملخص مبيعات الوردية وعدّ النقود وإغلاق الوردية.");

    public static LocalText ShiftSample { get; } = new("Sample data: open shift with {0} sales, {1}", "بيانات تجريبية: وردية مفتوحة فيها {0} عملية بيع، {1}");

    public static LocalText NoShift { get; } = new("Sample data: no open shift", "بيانات تجريبية: لا توجد وردية مفتوحة");
}
