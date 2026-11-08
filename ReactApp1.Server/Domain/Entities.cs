namespace ReactApp1.Server.Domain;

// ---- همهٔ مبالغ به «ریال» و Decimal(18,0) ذخیره می‌شوند؛ تبدیل به تومان فقط در UI ----

public class AppUser
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string FullName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public int? ParentId { get; set; }
    public Category? Parent { get; set; }
    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public string? IntroText { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Product> Products { get; set; } = [];
}

public class Brand
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string? LogoUrl { get; set; }
}

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Sku { get; set; } = "";
    public string? PartNumber { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public int? BrandId { get; set; }
    public Brand? Brand { get; set; }
    public string? Description { get; set; }
    public decimal SalePrice { get; set; }
    public decimal DiscountPercent { get; set; }          // 0..100
    public decimal PurchasePrice { get; set; }            // آخرین قیمت خرید
    public decimal AverageCost { get; set; }              // بهای تمام‌شدهٔ میانگین موزون
    public int StockQuantity { get; set; }                // مجموع انبارها؛ فقط از طریق سند تغییر می‌کند
    public int SoldCount { get; set; }                    // توسط سفارش‌های ارسال‌شده افزایش می‌یابد (مرحلهٔ سفارش)
    public string? Warranty { get; set; }
    public ProductCondition Condition { get; set; } = ProductCondition.New;
    public bool IsPublished { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? SeoKeywords { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public List<ProductImage> Images { get; set; } = [];
    public List<ProductSpecification> Specifications { get; set; } = [];
}

public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Url { get; set; } = "";
    public string? Alt { get; set; }
    public int SortOrder { get; set; }
}

public class ProductSpecification
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Name { get; set; } = "";
    public string Value { get; set; } = "";
}

/// طرف حساب: مشتری، تأمین‌کننده یا هر دو. بدون نیاز به حساب کاربری.
public class Party
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public bool IsCustomer { get; set; }
    public bool IsSupplier { get; set; }
    public string? Notes { get; set; }
    public int? UserId { get; set; }
    public int? ReferrerPartyId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Warehouse
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public class ProductStock
{
    public int ProductId { get; set; }
    public int WarehouseId { get; set; }
    public int Quantity { get; set; }
}

public class InventoryTransaction
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public StockReason Reason { get; set; }
    public int QuantityChange { get; set; }               // مثبت = ورود، منفی = خروج
    public decimal UnitCost { get; set; }
    public int BalanceAfter { get; set; }                 // موجودی کل محصول پس از این عملیات
    public string DocumentNumber { get; set; } = "";
    public string SourceType { get; set; } = "";
    public int? SourceId { get; set; }
    public int? PartyId { get; set; }
    public Party? Party { get; set; }
    public int CreatedByUserId { get; set; }
    public AppUser? CreatedBy { get; set; }
    public DateTime OccurredAt { get; set; }
    public string? Note { get; set; }
}

public class PurchaseReceipt
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int SupplierId { get; set; }
    public Party? Supplier { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string? SupplierInvoiceNumber { get; set; }
    public DateTime Date { get; set; }
    public ReceiptStatus Status { get; set; } = ReceiptStatus.Draft;
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }               // پرداخت اولیه هنگام تأیید
    public PaymentMethod? PaymentMethod { get; set; }
    public int? PaymentAccountId { get; set; }
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
    public string? IdempotencyKey { get; set; }
    public int? FinancialDocumentId { get; set; }
    public FinancialDocument? FinancialDocument { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? ReversedAt { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public List<PurchaseReceiptItem> Items { get; set; } = [];
}

public class PurchaseReceiptItem
{
    public int Id { get; set; }
    public int PurchaseReceiptId { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal DiscountAmount { get; set; }           // تخفیف کل ردیف
    public decimal LineTotal { get; set; }                // Quantity*UnitCost - DiscountAmount
}

public class FinancialAccount
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public AccountKind Kind { get; set; }
    public bool IsSystem { get; set; }                    // حساب‌های کنترلی (موجودی، بدهکاران، بستانکاران، ...)
    public bool IsActive { get; set; } = true;
}

public class FinancialDocument
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public DateTime Date { get; set; }
    public DocumentType Type { get; set; }
    public DocumentStatus Status { get; set; } = DocumentStatus.Posted;
    public string Description { get; set; } = "";
    public string SourceType { get; set; } = "";
    public int? SourceId { get; set; }
    public int? ReversalOfId { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<FinancialDocumentLine> Lines { get; set; } = [];
}

public class FinancialDocumentLine
{
    public int Id { get; set; }
    public int FinancialDocumentId { get; set; }
    public FinancialDocument? FinancialDocument { get; set; }
    public int AccountId { get; set; }
    public FinancialAccount? Account { get; set; }
    public int? PartyId { get; set; }
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public string? Description { get; set; }
}

/// دفتر طرف‌حساب. قرارداد واحد در کل سیستم: مانده = بدهکار − بستانکار
/// مانده مثبت = طلب مجموعه از طرف‌حساب، مانده منفی = بدهی مجموعه به طرف‌حساب.
public class PartyTransaction
{
    public int Id { get; set; }
    public int PartyId { get; set; }
    public int FinancialDocumentId { get; set; }
    public FinancialDocument? FinancialDocument { get; set; }
    public DateTime Date { get; set; }
    public DocumentType Type { get; set; }
    public string Description { get; set; } = "";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class Payment
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public PaymentDirection Direction { get; set; }
    public int PartyId { get; set; }
    public decimal Amount { get; set; }
    public PaymentMethod Method { get; set; }
    public int FinancialAccountId { get; set; }
    public string? ReferenceNumber { get; set; }
    public DateTime Date { get; set; }
    public int? PurchaseReceiptId { get; set; }
    public int? OrderId { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Posted;
    public string IdempotencyKey { get; set; } = "";
    public int FinancialDocumentId { get; set; }
    public FinancialDocument? FinancialDocument { get; set; }
    public string? Note { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AuditLog
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Action { get; set; } = "";
    public string Entity { get; set; } = "";
    public int? EntityId { get; set; }
    public string? Details { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}

public class Banner
{
    public int Id { get; set; }
    public BannerPlacement Placement { get; set; }
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }
    public string? DiscountLabel { get; set; }
    public string? ButtonText { get; set; }
    public string ImageUrl { get; set; } = "";
    public string? LinkUrl { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public class DocumentCounter
{
    public string Key { get; set; } = "";
    public long LastValue { get; set; }
}

public class DomainException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
