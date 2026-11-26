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
    long Price,
    int MinStock,
    bool IsActive,
    int Stock,
    bool IsLowStock);

public sealed record SaveProductRequest(
    string NameEn,
    string NameAr,
    int CategoryId,
    long Price,
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

public sealed record CreateSaleRequest(string Station, IReadOnlyList<SaleLineRequest> Lines, long? CashReceived = null);

public sealed record ReceiptLineDto(int ProductId, string NameEn, string NameAr, int Quantity, long UnitPrice, long LineTotal);

public sealed record ReceiptDto(
    long Id,
    long Number,
    DateTime At,
    string Station,
    int CashierId,
    string CashierName,
    int? ShiftId,
    IReadOnlyList<ReceiptLineDto> Lines,
    long Total,
    long? CashReceived,
    long? Change);

public sealed record SaleSummaryDto(long Id, long Number, DateTime At, string Station, int CashierId, string CashierName, long Total, int ItemCount);

public sealed record OpenShiftRequest(string Station, long OpeningCash = 0);

public sealed record CloseShiftRequest(long? ClosingCash = null, string? Note = null);

public sealed record ShiftDto(
    int Id,
    int CashierId,
    string CashierName,
    string Station,
    DateTime OpenedAt,
    DateTime? ClosedAt,
    long OpeningCash,
    long? ClosingCash,
    string? Note);

public sealed record ProductSalesDto(int ProductId, string NameEn, string NameAr, int Quantity, long Total);

public sealed record ShiftSummaryDto(
    ShiftDto Shift,
    int SaleCount,
    long SalesTotal,
    long ExpectedCash,
    long? CashDifference,
    IReadOnlyList<ProductSalesDto> Products);
