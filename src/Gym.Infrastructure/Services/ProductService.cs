namespace Gym.Infrastructure.Services;

/// <summary>Cafeteria categories and products. Product lists include the current stock.</summary>
public sealed class ProductService(GymDbContext db, GymClock clock)
{
    public async Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(bool includeInactive, CancellationToken ct = default) =>
        await db.ProductCategories.AsNoTracking()
            .Where(c => includeInactive || c.IsActive)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.NameEn)
            .Select(c => new CategoryDto(c.Id, c.NameEn, c.NameAr, c.SortOrder, c.IsActive))
            .ToListAsync(ct);

    public async Task<CategoryDto> CreateCategoryAsync(SaveCategoryRequest request, CancellationToken ct = default)
    {
        var category = new ProductCategory();
        Apply(category, request);
        db.ProductCategories.Add(category);
        await db.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task<CategoryDto> UpdateCategoryAsync(int id, SaveCategoryRequest request, CancellationToken ct = default)
    {
        var category = await db.ProductCategories.FindAsync([id], ct) ?? throw CategoryNotFound(id);
        Apply(category, request);
        await db.SaveChangesAsync(ct);
        return ToDto(category);
    }

    public async Task<IReadOnlyList<ProductDto>> ListAsync(string? text, int? categoryId, bool includeInactive, CancellationToken ct = default)
    {
        var query = db.Products.AsNoTracking().Where(p => includeInactive || p.IsActive);
        if (categoryId is { } category)
        {
            query = query.Where(p => p.CategoryId == category);
        }

        if (!string.IsNullOrWhiteSpace(text))
        {
            var term = text.Trim();
            var pattern = "%" + Guard.EscapeLike(term) + "%";
            query = query.Where(p => EF.Functions.ILike(p.NameEn, pattern) || EF.Functions.ILike(p.NameAr, pattern) || p.Barcode == term);
        }

        return await ToDtosAsync(query.OrderBy(p => p.Category!.SortOrder).ThenBy(p => p.NameEn), ct);
    }

    public async Task<ProductDto> GetAsync(int id, CancellationToken ct = default) =>
        (await ToDtosAsync(db.Products.AsNoTracking().Where(p => p.Id == id), ct)).SingleOrDefault() ?? throw ProductNotFound(id);

    public async Task<ProductDto> GetByBarcodeAsync(string barcode, CancellationToken ct = default)
    {
        var code = Product.NormalizeBarcode(barcode);
        var found = code is null
            ? null
            : (await ToDtosAsync(db.Products.AsNoTracking().Where(p => p.Barcode == code), ct)).SingleOrDefault();
        return found ?? throw new NotFoundException("product_not_found", $"No product has the barcode {code}.");
    }

    public async Task<ProductDto> CreateAsync(SaveProductRequest request, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var product = new Product { CreatedAt = now };
        await ApplyAsync(product, request, ct);
        product.UpdatedAt = now;
        db.Products.Add(product);
        await db.SaveChangesOrConflictAsync(ct);
        return await GetAsync(product.Id, ct);
    }

    public async Task<ProductDto> UpdateAsync(int id, SaveProductRequest request, CancellationToken ct = default)
    {
        var product = await db.Products.FindAsync([id], ct) ?? throw ProductNotFound(id);
        await ApplyAsync(product, request, ct);
        product.UpdatedAt = clock.UtcNow;
        await db.SaveChangesOrConflictAsync(ct);
        return await GetAsync(id, ct);
    }

    internal static NotFoundException ProductNotFound(int id) => new("product_not_found", $"Product {id} does not exist.");

    private static NotFoundException CategoryNotFound(int id) => new("category_not_found", $"Category {id} does not exist.");

    private static async Task<List<ProductDto>> ToDtosAsync(IQueryable<Product> query, CancellationToken ct)
    {
        var rows = await query
            .Select(p => new
            {
                Product = p,
                CategoryNameEn = p.Category!.NameEn,
                CategoryNameAr = p.Category.NameAr,
                Stock = p.Movements.Sum(m => m.Quantity),
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new ProductDto(
                r.Product.Id,
                r.Product.NameEn,
                r.Product.NameAr,
                r.Product.CategoryId,
                r.CategoryNameEn,
                r.CategoryNameAr,
                r.Product.Barcode,
                r.Product.Price,
                r.Product.MinStock,
                r.Product.IsActive,
                r.Stock,
                StockRules.IsLow(r.Stock, r.Product.MinStock)))
            .ToList();
    }

    private async Task ApplyAsync(Product product, SaveProductRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var nameEn = Guard.Optional(request.NameEn, "English name", Product.NameMaxLength);
        var nameAr = Guard.Optional(request.NameAr, "Arabic name", Product.NameMaxLength);
        if (nameEn is null && nameAr is null)
        {
            throw new DomainException("validation", "The product needs a name.");
        }

        product.NameEn = nameEn ?? nameAr!;
        product.NameAr = nameAr ?? nameEn!;
        product.Price = Guard.NotNegative(request.Price, "Price");
        product.MinStock = Guard.Range(request.MinStock, "Minimum stock", 0, 1_000_000);
        product.IsActive = request.IsActive;

        var barcode = Product.NormalizeBarcode(request.Barcode);
        if (barcode is { Length: > Product.BarcodeMaxLength })
        {
            throw new DomainException("validation", $"Barcode can be at most {Product.BarcodeMaxLength} characters.");
        }

        if (barcode is not null && await db.Products.AnyAsync(p => p.Barcode == barcode && p.Id != product.Id, ct))
        {
            throw DbErrors.ToConflict("ux_products_barcode");
        }

        product.Barcode = barcode;
        if (!await db.ProductCategories.AnyAsync(c => c.Id == request.CategoryId, ct))
        {
            throw CategoryNotFound(request.CategoryId);
        }

        product.CategoryId = request.CategoryId;
    }

    private static void Apply(ProductCategory category, SaveCategoryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var nameEn = Guard.Optional(request.NameEn, "English name", ProductCategory.NameMaxLength);
        var nameAr = Guard.Optional(request.NameAr, "Arabic name", ProductCategory.NameMaxLength);
        if (nameEn is null && nameAr is null)
        {
            throw new DomainException("validation", "The category needs a name.");
        }

        category.NameEn = nameEn ?? nameAr!;
        category.NameAr = nameAr ?? nameEn!;
        category.SortOrder = request.SortOrder;
        category.IsActive = request.IsActive;
    }

    private static CategoryDto ToDto(ProductCategory c) => new(c.Id, c.NameEn, c.NameAr, c.SortOrder, c.IsActive);
}
