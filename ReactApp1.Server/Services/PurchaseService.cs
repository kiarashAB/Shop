using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;

namespace ReactApp1.Server.Services;

public class PurchaseService(AppDbContext db, LedgerService ledger, PaymentService payments, InventoryService inventory)
{
    public async Task<PurchaseReceipt> CreateDraftAsync(CreatePurchaseDto dto, int userId, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(dto.IdempotencyKey))
        {
            var existing = await db.PurchaseReceipts.Include(r => r.Items).FirstOrDefaultAsync(r => r.IdempotencyKey == dto.IdempotencyKey, ct);
            if (existing is not null) return existing;
        }

        var supplier = await db.Parties.FirstOrDefaultAsync(p => p.Id == dto.SupplierId, ct) ?? throw new DomainException("تأمین‌کننده یافت نشد.", 404);
        if (!supplier.IsSupplier) throw new DomainException("طرف‌حساب انتخاب‌شده تأمین‌کننده نیست.");
        if (!await db.Warehouses.AnyAsync(w => w.Id == dto.WarehouseId && w.IsActive, ct)) throw new DomainException("انبار یافت نشد.", 404);
        if (dto.Items is null || dto.Items.Count == 0) throw new DomainException("رسید باید حداقل یک ردیف داشته باشد.");
        if (dto.Items.Select(i => i.ProductId).Distinct().Count() != dto.Items.Count) throw new DomainException("یک محصول نباید در چند ردیف تکرار شود.");

        var wanted = dto.Items.Select(i => i.ProductId).ToList();
        var existingIds = await db.Products.Where(p => wanted.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
        var receipt = new PurchaseReceipt
        {
            SupplierId = dto.SupplierId, WarehouseId = dto.WarehouseId, Date = dto.Date, SupplierInvoiceNumber = dto.SupplierInvoiceNumber,
            Notes = dto.Notes, IdempotencyKey = dto.IdempotencyKey, CreatedByUserId = userId
        };
        foreach (var i in dto.Items)
        {
            if (!existingIds.Contains(i.ProductId)) throw new DomainException($"محصول {i.ProductId} یافت نشد.", 404);
            if (i.Quantity <= 0) throw new DomainException("تعداد باید بزرگ‌تر از صفر باشد.");
            if (i.UnitCost < 0) throw new DomainException("قیمت خرید نمی‌تواند منفی باشد.");
            var gross = i.UnitCost * i.Quantity;
            if (i.DiscountAmount < 0 || i.DiscountAmount > gross) throw new DomainException("تخفیف ردیف نامعتبر است.");
            receipt.Items.Add(new PurchaseReceiptItem { ProductId = i.ProductId, Quantity = i.Quantity, UnitCost = i.UnitCost, DiscountAmount = i.DiscountAmount, LineTotal = gross - i.DiscountAmount });
        }
        receipt.TotalAmount = receipt.Items.Sum(i => i.LineTotal);
        if (receipt.TotalAmount <= 0) throw new DomainException("مبلغ کل رسید باید بزرگ‌تر از صفر باشد.");

        if (dto.PaidAmount < 0 || dto.PaidAmount > receipt.TotalAmount) throw new DomainException("مبلغ پرداخت‌شده باید بین صفر و مبلغ کل باشد.");
        if (dto.PaidAmount > 0)
        {
            if (dto.Method is null || dto.PaymentAccountId is null) throw new DomainException("برای پرداخت اولیه، روش پرداخت و حساب صندوق/بانک الزامی است.");
            var acc = await db.FinancialAccounts.FirstOrDefaultAsync(a => a.Id == dto.PaymentAccountId && a.IsActive && (a.Kind == AccountKind.Cash || a.Kind == AccountKind.Bank), ct)
                ?? throw new DomainException("حساب صندوق/بانک نامعتبر است.");
            receipt.PaidAmount = dto.PaidAmount; receipt.PaymentMethod = dto.Method; receipt.PaymentAccountId = acc.Id; receipt.PaymentReference = dto.PaymentReference;
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        receipt.Number = await ledger.NextNumberAsync("PR", ct);
        db.PurchaseReceipts.Add(receipt);
        ledger.Audit(userId, "PurchaseDraft", "PurchaseReceipt", null, receipt.Number);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return receipt;
    }

    /// تأیید رسید: موجودی + سند خرید + بدهی تأمین‌کننده + پرداخت اولیه، همه در یک تراکنش.
    /// فراخوانی مجدد روی رسید تأییدشده چیزی ثبت نمی‌کند. دو تأیید هم‌زمان با ConcurrencyStamp رد می‌شود.
    public async Task<PurchaseReceipt> ConfirmAsync(int id, int userId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var r = await db.PurchaseReceipts.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new DomainException("رسید یافت نشد.", 404);
        if (r.Status == ReceiptStatus.Confirmed) return r;
        if (r.Status == ReceiptStatus.Reversed) throw new DomainException("این رسید برگشت خورده و قابل تأیید نیست.", 409);

        var ids = r.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        var inventoryAcc = await ledger.GetSystemAccountAsync(AccountKind.Inventory, ct);
        var payableAcc = await ledger.GetSystemAccountAsync(AccountKind.Payable, ct);

        foreach (var item in r.Items)
        {
            var p = products[item.ProductId];
            var oldQty = Math.Max(p.StockQuantity, 0);
            var newQty = oldQty + item.Quantity;
            // میانگین موزون بهای تمام‌شده (بعد از تخفیف ردیف)
            p.AverageCost = Math.Round((oldQty * p.AverageCost + item.LineTotal) / newQty, 0, MidpointRounding.AwayFromZero);
            p.PurchasePrice = item.UnitCost;
            var netUnit = Math.Round(item.LineTotal / item.Quantity, 0, MidpointRounding.AwayFromZero);
            await inventory.MoveStockAsync(p, r.WarehouseId, item.Quantity, StockReason.Purchase, netUnit, r.Number, "PurchaseReceipt", r.Id, r.SupplierId, r.Date, null, userId, ct);
        }

        var doc = await ledger.PostAsync(DocumentType.Purchase, r.Date, $"خرید طبق رسید {r.Number}", "PurchaseReceipt", r.Id,
            [new(inventoryAcc.Id, r.TotalAmount, 0), new(payableAcc.Id, 0, r.TotalAmount, r.SupplierId)], userId, ct: ct);

        r.Status = ReceiptStatus.Confirmed;
        r.ConfirmedAt = DateTime.UtcNow;
        r.FinancialDocument = doc;
        r.ConcurrencyStamp = Guid.NewGuid();

        if (r.PaidAmount > 0)
        {
            await payments.PostPaymentAsync(new PaymentRequest(PaymentDirection.ToSupplier, r.SupplierId, r.PaidAmount, r.PaymentMethod!.Value,
                r.PaymentAccountId!.Value, r.PaymentReference, r.Date, r.Id, $"purchase-{r.Id}-initial", $"پرداخت اولیه رسید {r.Number}"), userId, ct);
        }

        ledger.Audit(userId, "PurchaseConfirm", "PurchaseReceipt", r.Id, $"{r.Number} / {r.TotalAmount:N0}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return r;
    }

    /// برگشت رسید: خروج کالا + سند معکوس. پرداخت‌های ثبت‌شده دست‌نخورده می‌مانند (به‌صورت مانده طلب از تأمین‌کننده دیده می‌شوند).
    public async Task<PurchaseReceipt> ReverseAsync(int id, string reason, int userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new DomainException("ثبت دلیل برگشت الزامی است.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var r = await db.PurchaseReceipts.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new DomainException("رسید یافت نشد.", 404);
        if (r.Status != ReceiptStatus.Confirmed) throw new DomainException("فقط رسید تأییدشده قابل برگشت است.", 409);

        var ids = r.Items.Select(i => i.ProductId).ToList();
        var products = await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, ct);
        foreach (var item in r.Items)
            await inventory.MoveStockAsync(products[item.ProductId], r.WarehouseId, -item.Quantity, StockReason.PurchaseReversal, item.UnitCost,
                r.Number, "PurchaseReceipt", r.Id, r.SupplierId, DateTime.UtcNow, reason, userId, ct);

        await ledger.ReverseDocumentAsync(r.FinancialDocumentId!.Value, reason, userId, ct);
        r.Status = ReceiptStatus.Reversed;
        r.ReversedAt = DateTime.UtcNow;
        r.ConcurrencyStamp = Guid.NewGuid();
        ledger.Audit(userId, "PurchaseReverse", "PurchaseReceipt", r.Id, reason);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return r;
    }
}
