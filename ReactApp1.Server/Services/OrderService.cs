using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;

namespace ReactApp1.Server.Services;

/// چرخهٔ سفارش. با ثبت سفارش، موجودی کم می‌شود (رزرو قطعی) و سند فروش ثبت می‌شود؛ با لغو، هر دو معکوس می‌شوند.
/// موجودی فقط با InventoryService.MoveStockAsync و سند مالی فقط با LedgerService.PostAsync تغییر می‌کند.
public class OrderService(AppDbContext db, LedgerService ledger, InventoryService inventory, PaymentService payments, CartService cart)
{
    static readonly Dictionary<OrderStatus, OrderStatus[]> Transitions = new()
    {
        [OrderStatus.PendingPayment] = [OrderStatus.Processing],
        [OrderStatus.Processing] = [OrderStatus.Shipped],
        [OrderStatus.Shipped] = [OrderStatus.Delivered],
    };

    static void AddHistory(Order o, OrderStatus? from, OrderStatus to, int? userId, string? note) =>
        o.History.Add(new OrderStatusHistory { FromStatus = from, ToStatus = to, ChangedByUserId = userId, Note = note, At = DateTime.UtcNow });

    Task<Order?> FindAsync(int id, CancellationToken ct) =>
        db.Orders.Include(o => o.Items).Include(o => o.Shipment).FirstOrDefaultAsync(o => o.Id == id, ct);

    /// ثبت سفارش از اقلام ارسالی. قیمت، تخفیف، ارسال و موجودی همگی دوباره سمت سرور محاسبه می‌شوند.
    /// تکرار درخواست با همان IdempotencyKey، همان سفارش را برمی‌گرداند و چیزی دوباره ثبت نمی‌کند.
    public async Task<Order> CreateAsync(int userId, CreateOrderDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.IdempotencyKey) || dto.IdempotencyKey.Length > 100)
            throw new DomainException("کلید یکتای عملیات (IdempotencyKey) الزامی است.");

        var existing = await db.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.IdempotencyKey == dto.IdempotencyKey, ct);
        if (existing is not null)
        {
            if (existing.UserId != userId) throw new DomainException("کلید عملیات تکراری است.", 409);
            return existing;
        }

        var items = CartService.Normalize(dto.Items);
        if (items.Count == 0) throw new DomainException("سبد خرید خالی است.");

        var address = await db.Addresses.FirstOrDefaultAsync(a => a.Id == dto.AddressId && a.UserId == userId && a.IsActive, ct)
            ?? throw new DomainException("آدرس انتخاب‌شده یافت نشد.", 404);
        var shipping = await db.ShippingMethods.FirstOrDefaultAsync(s => s.Id == dto.ShippingMethodId && s.IsActive, ct)
            ?? throw new DomainException("روش ارسال نامعتبر است.");
        var party = await db.Parties.FirstOrDefaultAsync(p => p.UserId == userId && p.IsCustomer, ct)
            ?? throw new DomainException("پروفایل مشتری یافت نشد.", 404);

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var number = await ledger.NextNumberAsync("OR", ct);
        var ids = items.Select(i => i.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id) && p.IsPublished).ToDictionaryAsync(p => p.Id, ct);
        var stocks = await db.ProductStocks
            .Where(s => ids.Contains(s.ProductId) && db.Warehouses.Any(w => w.Id == s.WarehouseId && w.IsActive))
            .ToListAsync(ct);
        var images = await db.ProductImages.AsNoTracking()
            .Where(i => ids.Contains(i.ProductId)).OrderBy(i => i.SortOrder)
            .Select(i => new { i.ProductId, i.Url }).ToListAsync(ct);

        var now = DateTime.UtcNow;
        var order = new Order
        {
            Number = number, UserId = userId, PartyId = party.Id, Status = OrderStatus.PendingPayment, IdempotencyKey = dto.IdempotencyKey,
            RecipientName = address.RecipientName, Phone = address.Phone, Province = address.Province, City = address.City,
            PostalCode = address.PostalCode, FullAddress = address.FullAddress,
            ShippingMethodId = shipping.Id, ShippingMethodName = shipping.Name,
            Note = Infrastructure.Validation.Optional(dto.Note, "توضیحات سفارش", 500), CreatedAt = now
        };

        foreach (var it in items)
        {
            if (!products.TryGetValue(it.ProductId, out var p))
                throw new DomainException($"محصول {it.ProductId} در دسترس نیست.", 404);
            var source = stocks.Where(s => s.ProductId == p.Id && s.Quantity >= it.Quantity).OrderByDescending(s => s.Quantity).FirstOrDefault()
                ?? throw new DomainException($"موجودی «{p.Name}» برای تعداد {it.Quantity} کافی نیست (موجود: {p.StockQuantity}).", 409);
            var unit = CartService.FinalUnitPrice(p.SalePrice, p.DiscountPercent);
            order.Items.Add(new OrderItem
            {
                ProductId = p.Id, ProductName = p.Name, Sku = p.Sku, Slug = p.Slug,
                ImageUrl = images.FirstOrDefault(i => i.ProductId == p.Id)?.Url,
                Quantity = it.Quantity, ListPrice = p.SalePrice, UnitPrice = unit, LineTotal = unit * it.Quantity,
                UnitCost = p.AverageCost, WarehouseId = source.WarehouseId
            });
        }

        order.Subtotal = order.Items.Sum(i => i.LineTotal);

        if (!string.IsNullOrWhiteSpace(dto.DiscountCode))
        {
            var (disc, amount, error) = await cart.EvaluateDiscountAsync(dto.DiscountCode, order.Subtotal, ct);
            if (disc is null) throw new DomainException(error ?? "کد تخفیف معتبر نیست.");
            // افزایش اتمیک شمارندهٔ استفاده؛ اگر ظرفیت همین لحظه تمام شده باشد صفر ردیف تغییر می‌کند.
            var changed = await db.Discounts
                .Where(d => d.Id == disc.Id && (d.MaxUses == null || d.UsedCount < d.MaxUses))
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.UsedCount, d => d.UsedCount + 1), ct);
            if (changed == 0) throw new DomainException("ظرفیت استفاده از این کد تخفیف تمام شده است.", 409);
            order.DiscountCode = disc.Code;
            order.DiscountAmount = amount;
        }

        order.ShippingCost = shipping.Price;
        order.Total = order.Subtotal - order.DiscountAmount + order.ShippingCost;
        if (order.Total <= 0) throw new DomainException("مبلغ سفارش نامعتبر است.");

        AddHistory(order, null, OrderStatus.PendingPayment, userId, "ثبت سفارش");
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);   // برای گرفتن Id سفارش (سند انبار و سند مالی به آن ارجاع می‌دهند)

        foreach (var item in order.Items)
            await inventory.MoveStockAsync(products[item.ProductId], item.WarehouseId, -item.Quantity, StockReason.Sale, item.UnitCost,
                order.Number, "Order", order.Id, party.Id, now, null, userId, ct);

        var receivable = await ledger.GetSystemAccountAsync(AccountKind.Receivable, ct);
        var sales = await ledger.GetSystemAccountAsync(AccountKind.Sales, ct);
        var lines = new List<PostingLine>
        {
            new(receivable.Id, order.Total, 0, party.Id),
            new(sales.Id, 0, order.Total)
        };
        var cost = order.Items.Sum(i => i.UnitCost * i.Quantity);
        if (cost > 0)
        {
            var cogs = await ledger.GetSystemAccountAsync(AccountKind.CostOfGoods, ct);
            var inventoryAcc = await ledger.GetSystemAccountAsync(AccountKind.Inventory, ct);
            lines.Add(new(cogs.Id, cost, 0));
            lines.Add(new(inventoryAcc.Id, 0, cost));
        }
        order.FinancialDocument = await ledger.PostAsync(DocumentType.Sale, now, $"فروش طبق سفارش {order.Number}", "Order", order.Id, lines, userId, ct: ct);

        var userCart = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (userCart is not null) db.CartItems.RemoveRange(userCart.Items);

        ledger.Audit(userId, "OrderCreate", "Order", order.Id, $"{order.Number} / {order.Total:N0}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return order;
    }

    /// لغو سفارش (فقط قبل از ارسال): موجودی برمی‌گردد و سند فروش با سند معکوس خنثی می‌شود.
    /// پرداخت‌های ثبت‌شده دست‌نخورده می‌مانند و به‌صورت مانده بستانکاری مشتری در دفتر او دیده می‌شوند.
    public async Task<Order> CancelAsync(int id, string reason, int userId, bool byCustomer, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("ثبت دلیل لغو الزامی است.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var o = await FindAsync(id, ct) ?? throw new DomainException("سفارش یافت نشد.", 404);
        if (o.Status == OrderStatus.Cancelled) return o;
        if (o.Status is not (OrderStatus.PendingPayment or OrderStatus.Processing))
            throw new DomainException("سفارش ارسال‌شده یا تحویل‌شده قابل لغو نیست.", 409);
        if (byCustomer && o.PaidAmount > 0)
            throw new DomainException("برای لغو سفارشِ پرداخت‌شده با پشتیبانی تماس بگیرید.", 409);

        var ids = o.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        foreach (var item in o.Items)
            await inventory.MoveStockAsync(products[item.ProductId], item.WarehouseId, item.Quantity, StockReason.Return, item.UnitCost,
                o.Number, "OrderCancel", o.Id, o.PartyId, DateTime.UtcNow, reason, userId, ct);

        if (o.FinancialDocumentId is int docId)
            await ledger.ReverseDocumentAsync(docId, $"لغو سفارش {o.Number}: {reason}", userId, ct);

        if (!string.IsNullOrEmpty(o.DiscountCode))
        {
            var code = o.DiscountCode;
            await db.Discounts.Where(d => d.Code == code && d.UsedCount > 0)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.UsedCount, d => d.UsedCount - 1), ct);
        }

        var from = o.Status;
        o.Status = OrderStatus.Cancelled;
        o.CancelledAt = DateTime.UtcNow;
        o.CancelReason = reason.Trim();
        o.ConcurrencyStamp = Guid.NewGuid();
        AddHistory(o, from, OrderStatus.Cancelled, userId, reason.Trim());
        ledger.Audit(userId, "OrderCancel", "Order", o.Id, $"{o.Number} / {reason.Trim()}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return o;
    }

    /// تغییر وضعیت مجاز: در انتظار پرداخت ← در حال پردازش، ارسال‌شده ← تحویل‌شده.
    /// «ارسال‌شده» فقط با ثبت اطلاعات ارسال (SetShipmentAsync) و «لغو» فقط با CancelAsync انجام می‌شود.
    public async Task<Order> ChangeStatusAsync(int id, OrderStatus to, string? note, int userId, CancellationToken ct)
    {
        if (to == OrderStatus.Cancelled) throw new DomainException("برای لغو سفارش از عملیات لغو استفاده کنید.");
        if (to == OrderStatus.Shipped) throw new DomainException("وضعیت «ارسال‌شده» با ثبت اطلاعات ارسال (کد رهگیری) تنظیم می‌شود.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var o = await FindAsync(id, ct) ?? throw new DomainException("سفارش یافت نشد.", 404);
        if (o.Status == to) return o;
        if (!Transitions.TryGetValue(o.Status, out var allowed) || !allowed.Contains(to))
            throw new DomainException($"تغییر وضعیت از {o.Status} به {to} مجاز نیست.", 409);

        var from = o.Status;
        o.Status = to;
        o.ConcurrencyStamp = Guid.NewGuid();
        AddHistory(o, from, to, userId, Infrastructure.Validation.Optional(note, "یادداشت", 500));
        ledger.Audit(userId, "OrderStatus", "Order", o.Id, $"{o.Number}: {from} → {to}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return o;
    }

    /// ثبت (یا اصلاح) اطلاعات ارسال. اگر سفارش «در حال پردازش» باشد به «ارسال‌شده» می‌رود و فروش محصولات شمرده می‌شود.
    public async Task<Order> SetShipmentAsync(int id, ShipmentDto dto, int userId, CancellationToken ct)
    {
        var carrier = Infrastructure.Validation.Required(dto.Carrier, "نام شرکت حمل", 100);
        var tracking = Infrastructure.Validation.Required(dto.TrackingCode, "کد رهگیری", 100);
        var note = Infrastructure.Validation.Optional(dto.Note, "یادداشت", 500);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var o = await FindAsync(id, ct) ?? throw new DomainException("سفارش یافت نشد.", 404);
        if (o.Status is not (OrderStatus.Processing or OrderStatus.Shipped))
            throw new DomainException("اطلاعات ارسال فقط برای سفارشِ «در حال پردازش» یا «ارسال‌شده» قابل ثبت است.", 409);

        if (o.Shipment is null) o.Shipment = new Shipment { Carrier = carrier, TrackingCode = tracking, Note = note, ShippedAt = DateTime.UtcNow };
        else { o.Shipment.Carrier = carrier; o.Shipment.TrackingCode = tracking; o.Shipment.Note = note; }

        if (o.Status == OrderStatus.Processing)
        {
            var ids = o.Items.Select(i => i.ProductId).ToList();
            var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
            foreach (var item in o.Items)
            {
                var p = products[item.ProductId];
                p.SoldCount += item.Quantity;
                p.ConcurrencyStamp = Guid.NewGuid();
            }
            o.Status = OrderStatus.Shipped;
            AddHistory(o, OrderStatus.Processing, OrderStatus.Shipped, userId, $"{carrier} / {tracking}");
        }
        o.ConcurrencyStamp = Guid.NewGuid();
        ledger.Audit(userId, "OrderShipment", "Order", o.Id, $"{o.Number}: {carrier} / {tracking}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return o;
    }

    /// ثبت دریافت وجه از مشتری برای سفارش (سند مالی: صندوق/بانک بدهکار، مشتری بستانکار).
    /// با پرداخت کامل، سفارشِ «در انتظار پرداخت» خودکار «در حال پردازش» می‌شود. تکرار با همان IdempotencyKey اثری ندارد.
    public async Task<Order> RegisterPaymentAsync(int id, OrderPaymentDto dto, int userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.IdempotencyKey)) throw new DomainException("کلید یکتای عملیات (IdempotencyKey) الزامی است.");
        if (dto.Amount <= 0) throw new DomainException("مبلغ باید بزرگ‌تر از صفر باشد.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var o = await FindAsync(id, ct) ?? throw new DomainException("سفارش یافت نشد.", 404);
        if (await db.Payments.AnyAsync(p => p.IdempotencyKey == dto.IdempotencyKey, ct)) return o;
        if (o.Status == OrderStatus.Cancelled) throw new DomainException("برای سفارش لغوشده نمی‌توان پرداخت ثبت کرد.", 409);
        var remaining = o.Total - o.PaidAmount;
        if (dto.Amount > remaining) throw new DomainException($"مبلغ از مانده سفارش بیشتر است (مانده: {remaining:N0} ریال).");

        await payments.PostPaymentAsync(new PaymentRequest(PaymentDirection.FromCustomer, o.PartyId, dto.Amount, dto.Method, dto.FinancialAccountId,
            dto.ReferenceNumber, dto.Date ?? DateTime.UtcNow, null, dto.IdempotencyKey, dto.Note ?? $"دریافت وجه سفارش {o.Number}", o.Id), userId, ct);

        o.PaidAmount += dto.Amount;
        o.ConcurrencyStamp = Guid.NewGuid();
        if (o.PaidAmount >= o.Total && o.Status == OrderStatus.PendingPayment)
        {
            o.Status = OrderStatus.Processing;
            AddHistory(o, OrderStatus.PendingPayment, OrderStatus.Processing, userId, "پرداخت کامل شد");
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return o;
    }
}
