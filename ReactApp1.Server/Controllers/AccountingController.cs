using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;
using ReactApp1.Server.Infrastructure;
using ReactApp1.Server.Services;

namespace ReactApp1.Server.Controllers;

[ApiController, Route("api"), Authorize]
public class AccountingController(AppDbContext db, LedgerService ledger, PaymentService payments, InventoryService inventory) : ControllerBase
{
    [HttpPost("payments/supplier"), Authorize(Roles = AppRoles.Finance)]
    public async Task<IActionResult> PaySupplier(PaymentDto d, CancellationToken ct)
    {
        var p = await payments.RegisterAsync(new(PaymentDirection.ToSupplier, d.PartyId, d.Amount, d.Method, d.FinancialAccountId, d.ReferenceNumber, d.Date, d.PurchaseReceiptId, d.IdempotencyKey, d.Note), User.GetUserId(), ct);
        return Ok(new { p.Id, p.Number, p.Amount, p.FinancialDocumentId });
    }

    [HttpPost("payments/customer"), Authorize(Roles = AppRoles.Finance)]
    public async Task<IActionResult> ReceiveFromCustomer(PaymentDto d, CancellationToken ct)
    {
        var p = await payments.RegisterAsync(new(PaymentDirection.FromCustomer, d.PartyId, d.Amount, d.Method, d.FinancialAccountId, d.ReferenceNumber, d.Date, null, d.IdempotencyKey, d.Note), User.GetUserId(), ct);
        return Ok(new { p.Id, p.Number, p.Amount, p.FinancialDocumentId });
    }

    [HttpGet("accounting/accounts"), Authorize(Roles = AppRoles.StockAndFinance)]
    public async Task<IActionResult> Accounts(CancellationToken ct) =>
        Ok(await db.FinancialAccounts.AsNoTracking().Where(a => a.IsActive).OrderBy(a => a.Code).Select(a => new { a.Id, a.Code, a.Name, a.Kind }).ToListAsync(ct));

    [HttpGet("accounting/trial-balance"), Authorize(Roles = AppRoles.Finance)]
    public async Task<IActionResult> TrialBalance(CancellationToken ct) => Ok(await ledger.GetTrialBalanceAsync(ct));

    [HttpGet("inventory/products/{productId:int}/kardex"), Authorize(Roles = AppRoles.StockAndFinance)]
    public async Task<IActionResult> Kardex(int productId, CancellationToken ct) =>
        Ok(await db.InventoryTransactions.AsNoTracking().Where(t => t.ProductId == productId).OrderBy(t => t.OccurredAt).ThenBy(t => t.Id)
            .Select(t => new
            {
                t.Id, t.OccurredAt, t.Reason, t.QuantityChange, t.UnitCost, t.BalanceAfter, t.DocumentNumber,
                Warehouse = t.Warehouse!.Name, Party = t.Party != null ? t.Party.Name : null, By = t.CreatedBy!.UserName, t.Note
            }).ToListAsync(ct));

    [HttpPost("inventory/issue"), Authorize(Roles = AppRoles.Stock)]
    public async Task<IActionResult> Issue(IssueStockDto dto, CancellationToken ct)
    {
        var t = await inventory.IssueAsync(dto, User.GetUserId(), ct);
        return Ok(new { t.Id, t.DocumentNumber, t.BalanceAfter });
    }

    [HttpGet("warehouses"), Authorize(Roles = AppRoles.StockAndFinance)]
    public async Task<IActionResult> Warehouses(CancellationToken ct) =>
        Ok(await db.Warehouses.AsNoTracking().Where(w => w.IsActive).Select(w => new { w.Id, w.Name }).ToListAsync(ct));
}
