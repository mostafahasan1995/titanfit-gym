namespace Gym.Contracts;

public sealed record CategoryDto(int Id, string NameEn, string NameAr, int SortOrder, bool IsActive);

public sealed record SaveCategoryRequest(string NameEn, string NameAr, int SortOrder = 0, bool IsActive = true);

public sealed record ProductDto(
    int Id,
    string NameEn,
    string NameAr,
    int CategoryId,
    string CategoryNameEn,
    string CategoryNameAr,
    string? Barcode,
    decimal Price,
    int MinStock,
    bool IsActive,
    int Stock,
    bool IsLowStock);

public sealed record SaveProductRequest(
    string NameEn,
    string NameAr,
    int CategoryId,
    decimal Price,
    string? Barcode = null,
    int MinStock = 0,
    bool IsActive = true);

public sealed record StockInRequest(int ProductId, int Quantity, string? Note = null);

/// <summary>Give either <see cref="Delta"/> (the change) or <see cref="CountedQuantity"/> (a stocktake).</summary>
public sealed record StockAdjustmentRequest(int ProductId, int? Delta = null, int? CountedQuantity = null, string? Note = null);

public sealed record StockLevelDto(int ProductId, string NameEn, string NameAr, int Stock, int MinStock, bool IsLow);

public sealed record StockMovementDto(
    long Id,
    int ProductId,
    string ProductNameEn,
    string ProductNameAr,
    int Quantity,
    StockMovementType Type,
    long? SaleId,
    string? Note,
    string StaffName,
    DateTime At);

public sealed record SaleLineRequest(int ProductId, int Quantity);

/// <summary>
/// A sale. <see cref="CashReceived"/> is in the currency of <see cref="Payment"/> (US dollars when it is null).
/// </summary>
public sealed record CreateSaleRequest(
    string Station,
    IReadOnlyList<SaleLineRequest> Lines,
    decimal? CashReceived = null,
    PaymentRequest? Payment = null);

public sealed record ReceiptLineDto(int ProductId, string NameEn, string NameAr, int Quantity, decimal UnitPrice, decimal LineTotal);

public sealed record ReceiptDto(
    long Id,
    long Number,
    DateTime At,
    string Station,
    int CashierId,
    string CashierName,
    int? ShiftId,
    IReadOnlyList<ReceiptLineDto> Lines,
    decimal Total,
    decimal? CashReceived,
    decimal? Change,
    Currency Currency = Currency.Usd,
    decimal PaidAmount = 0,
    decimal? ExchangeRate = null);

public sealed record SaleSummaryDto(
    long Id,
    long Number,
    DateTime At,
    string Station,
    int CashierId,
    string CashierName,
    decimal Total,
    int ItemCount,
    Currency Currency = Currency.Usd,
    decimal PaidAmount = 0);

public sealed record OpenShiftRequest(string Station, decimal OpeningCash = 0);

public sealed record CloseShiftRequest(decimal? ClosingCash = null, string? Note = null);

public sealed record ShiftDto(
    int Id,
    int CashierId,
    string CashierName,
    string Station,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    decimal OpeningCash,
    decimal? ClosingCash,
    string? Note);

public sealed record ProductSalesDto(int ProductId, string NameEn, string NameAr, int Quantity, decimal Total);

public sealed record ShiftSummaryDto(
    ShiftDto Shift,
    int SaleCount,
    decimal SalesTotal,
    decimal ExpectedCash,
    decimal? CashDifference,
    IReadOnlyList<ProductSalesDto> Products,
    CurrencyTotals? Paid = null);
