using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;
using ReactApp1.Server.Infrastructure;
using ReactApp1.Server.Services;

namespace ReactApp1.Server.Controllers;

[ApiController, Route("api/purchases"), Authorize(Roles = AppRoles.StockAndFinance)]
public class PurchasesController(AppDbContext db, PurchaseService purchases) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? supplierId, [FromQuery] ReceiptStatus? status, CancellationToken ct)
    {
        var q = db.PurchaseReceipts.AsNoTracking().AsQueryable();
        if (supplierId is not null) q = q.Where(r => r.SupplierId == supplierId);
        if (status is not null) q = q.Where(r => r.Status == status);
        return Ok(await q.OrderByDescending(r => r.Id).Take(200)
            .Select(r => new { r.Id, r.Number, r.Date, r.Status, Supplier = r.Supplier!.Name, r.SupplierInvoiceNumber, r.TotalAmount }).ToListAsync(ct));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var r = await db.PurchaseReceipts.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, x.Number, x.Date, x.Status, x.SupplierId, Supplier = x.Supplier!.Name, x.WarehouseId, x.SupplierInvoiceNumber, x.Notes,
                x.TotalAmount, PaidToDate = db.Payments.Where(p => p.PurchaseReceiptId == x.Id && p.Status == PaymentStatus.Posted).Sum(p => (decimal?)p.Amount) ?? 0,
                x.FinancialDocumentId,
                Items = x.Items.Select(i => new { i.ProductId, ProductName = i.Product!.Name, i.Quantity, i.UnitCost, i.DiscountAmount, i.LineTotal })
            }).FirstOrDefaultAsync(ct);
        if (r is null) return NotFound();
        return Ok(new { r.Id, r.Number, r.Date, r.Status, r.SupplierId, r.Supplier, r.WarehouseId, r.SupplierInvoiceNumber, r.Notes, r.TotalAmount, r.PaidToDate, Remaining = r.TotalAmount - r.PaidToDate, r.FinancialDocumentId, r.Items });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreatePurchaseDto dto, CancellationToken ct)
    {
        var r = await purchases.CreateDraftAsync(dto, User.GetUserId(), ct);
        return CreatedAtAction(nameof(Get), new { id = r.Id }, new { r.Id, r.Number, r.Status, r.TotalAmount });
    }

    [HttpPost("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int id, CancellationToken ct)
    {
        var r = await purchases.ConfirmAsync(id, User.GetUserId(), ct);
        return Ok(new { r.Id, r.Number, r.Status, r.TotalAmount, r.FinancialDocumentId });
    }

    [HttpPost("{id:int}/reverse"), Authorize(Roles = AppRoles.Finance)]
    public async Task<IActionResult> Reverse(int id, ReverseDto dto, CancellationToken ct)
    {
        var r = await purchases.ReverseAsync(id, dto.Reason, User.GetUserId(), ct);
        return Ok(new { r.Id, r.Number, r.Status });
    }
}
