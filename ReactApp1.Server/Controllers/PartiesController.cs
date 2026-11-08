using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;
using ReactApp1.Server.Infrastructure;
using ReactApp1.Server.Services;

namespace ReactApp1.Server.Controllers;

[ApiController, Route("api/parties"), Authorize(Roles = AppRoles.People)]
public class PartiesController(AppDbContext db, LedgerService ledger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, [FromQuery] bool? suppliers, [FromQuery] bool? customers, CancellationToken ct)
    {
        var q = db.Parties.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(p => p.Name.Contains(search) || (p.Phone != null && p.Phone.Contains(search)));
        if (suppliers == true) q = q.Where(p => p.IsSupplier);
        if (customers == true) q = q.Where(p => p.IsCustomer);
        return Ok(await q.OrderBy(p => p.Name).Take(200).Select(p => new
        {
            p.Id, p.Name, p.Phone, p.IsCustomer, p.IsSupplier,
            Balance = db.PartyTransactions.Where(t => t.PartyId == p.Id).Sum(t => (decimal?)(t.Debit - t.Credit)) ?? 0
        }).ToListAsync(ct));
    }

    [HttpPost]
    public async Task<IActionResult> Create(PartyDto d, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(d.Name)) throw new DomainException("نام الزامی است.");
        if (!d.IsCustomer && !d.IsSupplier) throw new DomainException("طرف‌حساب باید مشتری یا تأمین‌کننده (یا هر دو) باشد.");
        var p = new Party { Name = d.Name.Trim(), Phone = d.Phone, Email = d.Email, Address = d.Address, IsCustomer = d.IsCustomer, IsSupplier = d.IsSupplier, Notes = d.Notes };
        db.Parties.Add(p);
        ledger.Audit(User.GetUserId(), "PartyCreate", "Party", null, p.Name);
        await db.SaveChangesAsync(ct);
        return Ok(new { p.Id, p.Name });
    }

    /// دفتر گردش حساب. مانده = بدهکار − بستانکار؛ مثبت: طلب مجموعه، منفی: بدهی مجموعه.
    [HttpGet("{id:int}/ledger"), Authorize(Roles = AppRoles.Finance)]
    public async Task<IActionResult> Ledger(int id, [FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        if (!await db.Parties.AnyAsync(p => p.Id == id, ct)) return NotFound();
        var l = await ledger.GetPartyLedgerAsync(id, from, to, ct);
        return Ok(new { l.OpeningBalance, l.Balance, Status = l.Balance > 0 ? "طلب" : l.Balance < 0 ? "بدهی" : "تسویه", l.Rows });
    }
}
