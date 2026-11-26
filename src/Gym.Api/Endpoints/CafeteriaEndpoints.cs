using System.Security.Claims;
using Gym.Api.Auth;
using Gym.Api.Live;

namespace Gym.Api.Endpoints;

internal static class CafeteriaEndpoints
{
    public static void MapCafeteriaEndpoints(this RouteGroupBuilder api)
    {
        var categories = api.MapGroup("/product-categories").WithTags("Cafeteria");

        categories.MapGet("/", (bool? includeInactive, ProductService service, CancellationToken ct) =>
            service.ListCategoriesAsync(includeInactive ?? false, ct)).RequireAuthorization(Policies.AnyStaff);

        categories.MapPost("/", async (SaveCategoryRequest request, ProductService service, CancellationToken ct) =>
        {
            var category = await service.CreateCategoryAsync(request, ct);
            return Results.Created($"/api/product-categories/{category.Id}", category);
        }).RequireAuthorization(Policies.Cafeteria);

        categories.MapPut("/{id:int}", (int id, SaveCategoryRequest request, ProductService service, CancellationToken ct) =>
            service.UpdateCategoryAsync(id, request, ct)).RequireAuthorization(Policies.Cafeteria);

        var products = api.MapGroup("/products").WithTags("Cafeteria");

        products.MapGet("/", (string? q, int? categoryId, bool? includeInactive, ProductService service, CancellationToken ct) =>
            service.ListAsync(q, categoryId, includeInactive ?? false, ct)).RequireAuthorization(Policies.AnyStaff);

        products.MapGet("/{id:int}", (int id, ProductService service, CancellationToken ct) => service.GetAsync(id, ct))
            .RequireAuthorization(Policies.AnyStaff);

        products.MapGet("/by-barcode/{barcode}", (string barcode, ProductService service, CancellationToken ct) => service.GetByBarcodeAsync(barcode, ct))
            .RequireAuthorization(Policies.AnyStaff);

        products.MapPost("/", async (SaveProductRequest request, ProductService service, LiveUpdates live, CancellationToken ct) =>
        {
            var product = await service.CreateAsync(request, ct);
            await live.ProductChangedAsync(product.Id);
            return Results.Created($"/api/products/{product.Id}", product);
        }).RequireAuthorization(Policies.Cafeteria);

        products.MapPut("/{id:int}", async (int id, SaveProductRequest request, ProductService service, LiveUpdates live, CancellationToken ct) =>
        {
            var product = await service.UpdateAsync(id, request, ct);
            await live.ProductChangedAsync(id);
            return product;
        }).RequireAuthorization(Policies.Cafeteria);

        var stock = api.MapGroup("/stock").WithTags("Cafeteria").RequireAuthorization(Policies.Cafeteria);

        stock.MapGet("/", (bool? lowOnly, StockService service, CancellationToken ct) => service.GetLevelsAsync(null, lowOnly ?? false, ct));

        stock.MapPost("/in", async (StockInRequest request, ClaimsPrincipal user, StockService service, LiveUpdates live, CancellationToken ct) =>
        {
            var level = await service.StockInAsync(request, user.StaffId(), ct);
            await live.StockChangedAsync([level]);
            return level;
        });

        // Either "delta" (the change) or "countedQuantity" (a stocktake). The stock cannot go below zero.
        stock.MapPost("/adjustments", async (StockAdjustmentRequest request, ClaimsPrincipal user, StockService service, LiveUpdates live, CancellationToken ct) =>
        {
            var level = await service.AdjustAsync(request, user.StaffId(), ct);
            await live.StockChangedAsync([level]);
            return level;
        });

        stock.MapGet("/movements", (int? productId, DateOnly? from, DateOnly? to, StockService service, GymClock clock, CancellationToken ct) =>
            service.ListMovementsAsync(productId, from ?? clock.Today.AddDays(-30), to ?? clock.Today, ct));

        var sales = api.MapGroup("/sales").WithTags("Cafeteria").RequireAuthorization(Policies.Cafeteria);

        // One transaction: lines + stock movements. A line above the current stock is refused (insufficient_stock).
        sales.MapPost("/", async (CreateSaleRequest request, ClaimsPrincipal user, SalesService service, StockService stockService, LiveUpdates live, CancellationToken ct) =>
        {
            var receipt = await service.CreateAsync(request, user.StaffId(), ct);
            await live.SaleCompletedAsync(receipt);
            await live.StockChangedAsync(await stockService.GetLevelsAsync(receipt.Lines.Select(l => l.ProductId).ToList(), lowOnly: false, ct));
            return Results.Created($"/api/sales/{receipt.Id}", receipt);
        });

        sales.MapGet("/{id:long}", (long id, SalesService service, CancellationToken ct) => service.GetReceiptAsync(id, ct));

        // Cashiers see their own sales; an admin sees all (or one cashier's).
        sales.MapGet("/", (DateOnly? from, DateOnly? to, int? cashierId, ClaimsPrincipal user, SalesService service, GymClock clock, CancellationToken ct) =>
            service.ListAsync(from ?? clock.Today, to ?? from ?? clock.Today, user.IsAdmin() ? cashierId : user.StaffId(), ct));

        var shifts = api.MapGroup("/shifts").WithTags("Cafeteria").RequireAuthorization(Policies.Cafeteria);

        shifts.MapPost("/open", (OpenShiftRequest request, ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
            service.OpenAsync(request, user.StaffId(), ct));

        shifts.MapGet("/current", async (ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
            await service.GetCurrentAsync(user.StaffId(), ct) is { } shift ? Results.Ok(shift) : Results.NoContent());

        shifts.MapPost("/{id:int}/close", (int id, CloseShiftRequest request, ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
            service.CloseAsync(id, request, user.StaffId(), user.IsAdmin(), ct));

        shifts.MapGet("/{id:int}/summary", (int id, ClaimsPrincipal user, ShiftService service, CancellationToken ct) =>
            service.GetSummaryAsync(id, user.StaffId(), user.IsAdmin(), ct));

        shifts.MapGet("/", (DateOnly? from, DateOnly? to, int? cashierId, ClaimsPrincipal user, ShiftService service, GymClock clock, CancellationToken ct) =>
            service.ListAsync(from ?? clock.Today, to ?? from ?? clock.Today, user.IsAdmin() ? cashierId : user.StaffId(), ct));
    }
}
