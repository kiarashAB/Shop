using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;
using ReactApp1.Server.Infrastructure;
using ReactApp1.Server.Services;

namespace ReactApp1.Server.Controllers;

[ApiController, Route("api")]
public class ShippingController(AppDbContext db) : ControllerBase
{
    /// روش‌های ارسال فعال (عمومی؛ برای محاسبهٔ هزینه در سبد و تسویه‌حساب).
    [HttpGet("shipping-methods"), AllowAnonymous]
    public async Task<IActionResult> Active(CancellationToken ct) =>
        Ok(await db.ShippingMethods.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .Select(s => new { s.Id, s.Name, s.Description, s.Price }).ToListAsync(ct));

    [HttpGet("admin/shipping-methods"), Authorize(Roles = AppRoles.Settings)]
    public async Task<IActionResult> All(CancellationToken ct) =>
        Ok(await db.ShippingMethods.AsNoTracking().OrderBy(s => s.SortOrder).ThenBy(s => s.Id).ToListAsync(ct));

    static void Apply(ShippingMethod m, ShippingMethodDto dto)
    {
        if (dto.Price < 0) throw new DomainException("هزینه ارسال نمی‌تواند منفی باشد.");
        m.Name = Validation.Required(dto.Name, "نام روش ارسال", 100);
        m.Description = Validation.Optional(dto.Description, "توضیحات", 300);
        m.Price = dto.Price;
        m.SortOrder = dto.SortOrder;
        m.IsActive = dto.IsActive;
    }

    [HttpPost("admin/shipping-methods"), Authorize(Roles = AppRoles.Settings)]
    public async Task<IActionResult> Create(ShippingMethodDto dto, CancellationToken ct)
    {
        var m = new ShippingMethod();
        Apply(m, dto);
        db.ShippingMethods.Add(m);
        await db.SaveChangesAsync(ct);
        return Ok(m);
    }

    [HttpPut("admin/shipping-methods/{id:int}"), Authorize(Roles = AppRoles.Settings)]
    public async Task<IActionResult> Update(int id, ShippingMethodDto dto, CancellationToken ct)
    {
        var m = await db.ShippingMethods.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new DomainException("روش ارسال یافت نشد.", 404);
        Apply(m, dto);
        await db.SaveChangesAsync(ct);
        return Ok(m);
    }
}

/// مدیریت کدهای تخفیف. حذف وجود ندارد؛ کد فقط غیرفعال می‌شود تا سوابق سفارش‌ها حفظ شود.
[ApiController, Route("api/admin/discounts"), Authorize(Roles = AppRoles.Discounts)]
public class DiscountsAdminController(AppDbContext db) : ControllerBase
{
    static void Apply(Discount d, DiscountDto dto)
    {
        var code = Validation.Required(dto.Code, "کد تخفیف", 40).ToUpperInvariant();
        if (code.Any(char.IsWhiteSpace)) throw new DomainException("کد تخفیف نباید فاصله داشته باشد.");
        if (dto.Type == DiscountType.Percent && (dto.Value < 1 || dto.Value > 100 || dto.Value != decimal.Truncate(dto.Value)))
            throw new DomainException("درصد تخفیف باید عدد صحیح بین ۱ تا ۱۰۰ باشد.");
        if (dto.Type == DiscountType.FixedAmount && dto.Value <= 0) throw new DomainException("مبلغ تخفیف باید بزرگ‌تر از صفر باشد.");
        if (dto.MinOrderAmount < 0) throw new DomainException("حداقل مبلغ سفارش نمی‌تواند منفی باشد.");
        if (dto.MaxUses is < 1) throw new DomainException("سقف دفعات استفاده باید حداقل ۱ باشد.");
        if (dto.StartsAt is not null && dto.EndsAt is not null && dto.EndsAt < dto.StartsAt) throw new DomainException("پایان بازه قبل از شروع آن است.");
        d.Code = code; d.Type = dto.Type; d.Value = dto.Value; d.MinOrderAmount = dto.MinOrderAmount;
        d.MaxUses = dto.MaxUses; d.StartsAt = dto.StartsAt; d.EndsAt = dto.EndsAt; d.IsActive = dto.IsActive;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) =>
        Ok(await db.Discounts.AsNoTracking().OrderByDescending(d => d.Id).ToListAsync(ct));

    [HttpPost]
    public async Task<IActionResult> Create(DiscountDto dto, CancellationToken ct)
    {
        var d = new Discount();
        Apply(d, dto);
        if (await db.Discounts.AnyAsync(x => x.Code == d.Code, ct)) throw new DomainException("این کد قبلاً ثبت شده است.", 409);
        db.Discounts.Add(d);
        await db.SaveChangesAsync(ct);
        return Ok(d);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, DiscountDto dto, CancellationToken ct)
    {
        var d = await db.Discounts.FirstOrDefaultAsync(x => x.Id == id, ct) ?? throw new DomainException("کد تخفیف یافت نشد.", 404);
        Apply(d, dto);
        if (await db.Discounts.AnyAsync(x => x.Code == d.Code && x.Id != id, ct)) throw new DomainException("این کد قبلاً ثبت شده است.", 409);
        await db.SaveChangesAsync(ct);
        return Ok(d);
    }
}
