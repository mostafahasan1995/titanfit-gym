using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Gym.Infrastructure.Persistence.Configurations;

internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> b)
    {
        b.ToTable("product_categories");
        b.HasKey(c => c.Id);
        b.Property(c => c.NameEn).HasMaxLength(ProductCategory.NameMaxLength);
        b.Property(c => c.NameAr).HasMaxLength(ProductCategory.NameMaxLength);
    }
}

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.ToTable("products", t =>
        {
            t.HasCheckConstraint("ck_products_price", "price >= 0");
            t.HasCheckConstraint("ck_products_min_stock", "min_stock >= 0");
        });
        b.HasKey(p => p.Id);
        b.Property(p => p.NameEn).HasMaxLength(Product.NameMaxLength);
        b.Property(p => p.NameAr).HasMaxLength(Product.NameMaxLength);
        b.Property(p => p.Barcode).HasMaxLength(Product.BarcodeMaxLength);
        b.HasIndex(p => p.Barcode).IsUnique().HasDatabaseName("ux_products_barcode");
        b.HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.ToTable("stock_movements", t => t.HasCheckConstraint("ck_stock_movements_quantity", "quantity <> 0"));
        b.HasKey(m => m.Id);
        b.Property(m => m.Type).HasConversion<string>().HasMaxLength(32);
        b.Property(m => m.Note).HasMaxLength(StockMovement.NoteMaxLength);
        b.HasOne(m => m.Product).WithMany(p => p.Movements).HasForeignKey(m => m.ProductId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(m => m.Sale).WithMany().HasForeignKey(m => m.SaleId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(m => m.Staff).WithMany().HasForeignKey(m => m.StaffId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(m => m.At);
    }
}

internal sealed class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> b)
    {
        b.ToTable("sales", t => t.HasCheckConstraint("ck_sales_total", "total >= 0"));
        b.HasKey(s => s.Id);
        b.Property(s => s.Number).HasDefaultValueSql($"nextval('{GymDbContext.SaleNumberSequence}')");
        b.HasIndex(s => s.Number).IsUnique().HasDatabaseName("ux_sales_number");
        b.Property(s => s.Station).HasMaxLength(CheckIn.StationMaxLength);
        b.Property(s => s.Currency).HasConversion<string>().HasMaxLength(8).HasDefaultValue(Currency.Usd).HasSentinel((Currency)(-1));
        b.HasOne(s => s.Cashier).WithMany().HasForeignKey(s => s.CashierId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(s => s.Shift).WithMany().HasForeignKey(s => s.ShiftId).OnDelete(DeleteBehavior.Restrict);
        b.HasMany(s => s.Lines).WithOne(l => l.Sale).HasForeignKey(l => l.SaleId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(s => s.At);
    }
}

internal sealed class SaleLineConfiguration : IEntityTypeConfiguration<SaleLine>
{
    public void Configure(EntityTypeBuilder<SaleLine> b)
    {
        b.ToTable("sale_lines", t => t.HasCheckConstraint("ck_sale_lines_quantity", "quantity > 0"));
        b.HasKey(l => l.Id);
        b.HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ShiftConfiguration : IEntityTypeConfiguration<Shift>
{
    public void Configure(EntityTypeBuilder<Shift> b)
    {
        b.ToTable("shifts");
        b.HasKey(s => s.Id);
        b.Property(s => s.Station).HasMaxLength(CheckIn.StationMaxLength);
        b.Property(s => s.Note).HasMaxLength(Shift.NoteMaxLength);
        b.HasOne(s => s.Cashier).WithMany().HasForeignKey(s => s.CashierId).OnDelete(DeleteBehavior.Restrict);

        // One open shift per cashier.
        b.HasIndex(s => s.CashierId).IsUnique().HasFilter("closed_at IS NULL").HasDatabaseName("ux_shifts_open_per_cashier");
        b.HasIndex(s => s.OpenedAt);
    }
}
