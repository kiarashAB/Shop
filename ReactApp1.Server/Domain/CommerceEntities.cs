namespace ReactApp1.Server.Domain;

// ---- فروشگاه: آدرس، سبد، ارسال، تخفیف، سفارش. مبالغ «ریال» و Decimal(18,0) (تبدیل به تومان فقط در UI) ----

public class Address
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = "";
    public string RecipientName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Province { get; set; } = "";
    public string City { get; set; } = "";
    public string? PostalCode { get; set; }
    public string FullAddress { get; set; } = "";
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;          // حذف منطقی؛ سفارش‌ها اسنپ‌شات آدرس دارند
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// سبد خرید کاربر واردشده. فقط شناسهٔ محصول و تعداد؛ قیمت و موجودی همیشه سمت سرور دوباره محاسبه می‌شود.
public class Cart
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public List<CartItem> Items { get; set; } = [];
}

public class CartItem
{
    public int Id { get; set; }
    public int CartId { get; set; }
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}

public class ShippingMethod
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

/// Value: برای Percent عدد صحیح ۱ تا ۱۰۰؛ برای FixedAmount مبلغ به ریال.
public class Discount
{
    public int Id { get; set; }
    public string Code { get; set; } = "";               // همیشه با حروف بزرگ ذخیره می‌شود
    public DiscountType Type { get; set; }
    public decimal Value { get; set; }
    public decimal MinOrderAmount { get; set; }
    public int? MaxUses { get; set; }
    public int UsedCount { get; set; }
    public DateTime? StartsAt { get; set; }
    public DateTime? EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Order
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int UserId { get; set; }
    public int PartyId { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.PendingPayment;

    // اسنپ‌شات آدرس و روش ارسال در لحظهٔ ثبت سفارش
    public string RecipientName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Province { get; set; } = "";
    public string City { get; set; } = "";
    public string? PostalCode { get; set; }
    public string FullAddress { get; set; } = "";
    public int? ShippingMethodId { get; set; }
    public string ShippingMethodName { get; set; } = "";

    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal ShippingCost { get; set; }
    public decimal Total { get; set; }
    public decimal PaidAmount { get; set; }
    public string? DiscountCode { get; set; }
    public string? Note { get; set; }

    public string IdempotencyKey { get; set; } = "";
    public int? FinancialDocumentId { get; set; }        // سند فروش (مشتری بدهکار / فروش + بهای تمام‌شده / موجودی)
    public FinancialDocument? FinancialDocument { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public List<OrderItem> Items { get; set; } = [];
    public List<OrderStatusHistory> History { get; set; } = [];
    public Shipment? Shipment { get; set; }
}

public class OrderItem
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = "";
    public string Sku { get; set; } = "";
    public string? Slug { get; set; }
    public string? ImageUrl { get; set; }
    public int Quantity { get; set; }
    public decimal ListPrice { get; set; }               // قیمت قبل از تخفیف محصول
    public decimal UnitPrice { get; set; }               // قیمت نهایی واحد
    public decimal LineTotal { get; set; }
    public decimal UnitCost { get; set; }                // بهای تمام‌شدهٔ میانگین در لحظهٔ فروش
    public int WarehouseId { get; set; }
}

public class OrderStatusHistory
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public OrderStatus? FromStatus { get; set; }
    public OrderStatus ToStatus { get; set; }
    public int? ChangedByUserId { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
}

public class Shipment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string Carrier { get; set; } = "";
    public string TrackingCode { get; set; } = "";
    public DateTime ShippedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
}
