using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Domain;

namespace ReactApp1.Server.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductImage> ProductImages => Set<ProductImage>();
    public DbSet<ProductSpecification> ProductSpecifications => Set<ProductSpecification>();
    public DbSet<Party> Parties => Set<Party>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<ProductStock> ProductStocks => Set<ProductStock>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<PurchaseReceipt> PurchaseReceipts => Set<PurchaseReceipt>();
    public DbSet<PurchaseReceiptItem> PurchaseReceiptItems => Set<PurchaseReceiptItem>();
    public DbSet<FinancialAccount> FinancialAccounts => Set<FinancialAccount>();
    public DbSet<FinancialDocument> FinancialDocuments => Set<FinancialDocument>();
    public DbSet<FinancialDocumentLine> FinancialDocumentLines => Set<FinancialDocumentLine>();
    public DbSet<PartyTransaction> PartyTransactions => Set<PartyTransaction>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Banner> Banners => Set<Banner>();
    public DbSet<DocumentCounter> DocumentCounters => Set<DocumentCounter>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        b.Properties<decimal>().HavePrecision(18, 0);   // ریال، بدون اعشار
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        // هیچ حذف آبشاری‌ای وجود ندارد: اسناد و تاریخچه نباید با حذف رکورد دیگر پاک شوند.
        foreach (var fk in m.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;

        m.Entity<AppUser>().HasIndex(x => x.UserName).IsUnique();
        m.Entity<Category>().HasIndex(x => x.Slug).IsUnique();
        m.Entity<Brand>().HasIndex(x => x.Slug).IsUnique();

        m.Entity<Product>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => x.Sku).IsUnique();
            e.Property(x => x.DiscountPercent).HasPrecision(5, 2);
            e.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        });

        m.Entity<ProductStock>().HasKey(x => new { x.ProductId, x.WarehouseId });
        m.Entity<DocumentCounter>().HasKey(x => x.Key);

        m.Entity<PurchaseReceipt>(e =>
        {
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique();   // SQL Server: فیلتر IS NOT NULL به‌طور خودکار
            e.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();
        });

        m.Entity<FinancialAccount>().HasIndex(x => x.Code).IsUnique();
        m.Entity<FinancialDocument>().HasIndex(x => x.Number).IsUnique();
        m.Entity<Payment>(e =>
        {
            e.HasIndex(x => x.Number).IsUnique();
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
        });

        m.Entity<PartyTransaction>().HasIndex(x => new { x.PartyId, x.Date });
        m.Entity<InventoryTransaction>().HasIndex(x => new { x.ProductId, x.OccurredAt });
        m.Entity<FinancialDocumentLine>().HasIndex(x => x.AccountId);

        // کلیدهای خارجی بدون Navigation (برای یکپارچگی داده)
        m.Entity<InventoryTransaction>().HasOne(t => t.CreatedBy).WithMany().HasForeignKey(t => t.CreatedByUserId);
        m.Entity<Payment>().HasOne<Party>().WithMany().HasForeignKey(p => p.PartyId);
        m.Entity<Payment>().HasOne<FinancialAccount>().WithMany().HasForeignKey(p => p.FinancialAccountId);
        m.Entity<Payment>().HasOne<PurchaseReceipt>().WithMany().HasForeignKey(p => p.PurchaseReceiptId);
        m.Entity<PartyTransaction>().HasOne<Party>().WithMany().HasForeignKey(t => t.PartyId);
        m.Entity<FinancialDocumentLine>().HasOne<Party>().WithMany().HasForeignKey(l => l.PartyId);
        m.Entity<ProductStock>().HasOne<Product>().WithMany().HasForeignKey(s => s.ProductId);
        m.Entity<ProductStock>().HasOne<Warehouse>().WithMany().HasForeignKey(s => s.WarehouseId);
        m.Entity<PurchaseReceipt>().HasOne<FinancialAccount>().WithMany().HasForeignKey(r => r.PaymentAccountId);
    }
}
