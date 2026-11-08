using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;

namespace ReactApp1.Server.Services;

public class InventoryService(AppDbContext db, LedgerService ledger)
{
    /// تنها راه تغییر موجودی. تراکنش و SaveChanges با فراخوان است.
    public async Task<InventoryTransaction> MoveStockAsync(Product p, int warehouseId, int delta, StockReason reason, decimal unitCost,
        string documentNumber, string sourceType, int? sourceId, int? partyId, DateTime date, string? note, int userId, CancellationToken ct)
    {
        if (delta == 0) throw new DomainException("تعداد نمی‌تواند صفر باشد.");
        var stock = await db.ProductStocks.FindAsync([p.Id, warehouseId], ct);
        if (stock is null)
        {
            stock = new ProductStock { ProductId = p.Id, WarehouseId = warehouseId };
            db.ProductStocks.Add(stock);
        }
        if (stock.Quantity + delta < 0)
            throw new DomainException($"موجودی انبار برای «{p.Name}» کافی نیست (موجود: {stock.Quantity}).", 409);

        stock.Quantity += delta;
        p.StockQuantity += delta;
        p.ConcurrencyStamp = Guid.NewGuid();

        var t = new InventoryTransaction
        {
            ProductId = p.Id, WarehouseId = warehouseId, Reason = reason, QuantityChange = delta, UnitCost = unitCost,
            BalanceAfter = p.StockQuantity, DocumentNumber = documentNumber, SourceType = sourceType, SourceId = sourceId,
            PartyId = partyId, CreatedByUserId = userId, OccurredAt = date, Note = note
        };
        db.InventoryTransactions.Add(t);
        return t;
    }

    /// خروج غیرفروش: مصرف داخلی، خرابی، ضایعات. هزینه به حساب «ضایعات و مصرف داخلی» می‌رود.
    public async Task<InventoryTransaction> IssueAsync(IssueStockDto dto, int userId, CancellationToken ct)
    {
        if (dto.Reason is not (StockReason.InternalUse or StockReason.Damage or StockReason.Waste))
            throw new DomainException("علت خروج مجاز نیست (مصرف داخلی، خرابی یا ضایعات).");
        if (dto.Quantity <= 0) throw new DomainException("تعداد باید بزرگ‌تر از صفر باشد.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var p = await db.Products.FirstOrDefaultAsync(x => x.Id == dto.ProductId, ct) ?? throw new DomainException("محصول یافت نشد.", 404);
        if (!await db.Warehouses.AnyAsync(w => w.Id == dto.WarehouseId && w.IsActive, ct)) throw new DomainException("انبار یافت نشد.", 404);

        var date = dto.Date ?? DateTime.UtcNow;
        var number = await ledger.NextNumberAsync("WO", ct);
        var cost = p.AverageCost;
        var t = await MoveStockAsync(p, dto.WarehouseId, -dto.Quantity, dto.Reason, cost, number, "StockWriteOff", null, null, date, dto.Note, userId, ct);

        var value = cost * dto.Quantity;
        if (value > 0)
        {
            var expense = await ledger.GetSystemAccountAsync(AccountKind.OtherExpense, ct);
            var inventory = await ledger.GetSystemAccountAsync(AccountKind.Inventory, ct);
            await ledger.PostAsync(DocumentType.StockWriteOff, date, $"خروج کالا ({dto.Reason}) سند {number}", "StockWriteOff", null,
                [new(expense.Id, value, 0), new(inventory.Id, 0, value)], userId, ct: ct);
        }
        ledger.Audit(userId, "StockIssue", "Product", p.Id, $"{dto.Reason} x{dto.Quantity} / {number}");
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return t;
    }
}
