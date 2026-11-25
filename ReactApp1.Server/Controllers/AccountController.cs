using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;
using ReactApp1.Server.Infrastructure;
using ReactApp1.Server.Services;

namespace ReactApp1.Server.Controllers;

/// پنل مشتری: پروفایل، تغییر رمز و آدرس‌ها. هر مشتری فقط به داده‌های خودش دسترسی دارد (شناسه از کوکی نشست می‌آید، نه از ورودی).
[ApiController, Route("api/me"), Authorize(Roles = nameof(UserRole.Customer))]
public class AccountController(AppDbContext db, IPasswordHasher<AppUser> hasher) : ControllerBase
{
    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken ct)
    {
        var uid = User.GetUserId();
        var u = await db.Users.AsNoTracking().FirstOrDefaultAsync(x => x.Id == uid, ct) ?? throw new DomainException("کاربر یافت نشد.", 404);
        var email = await db.Parties.AsNoTracking().Where(p => p.UserId == uid && p.IsCustomer).Select(p => p.Email).FirstOrDefaultAsync(ct);
        return Ok(new { fullName = u.FullName, phone = u.UserName, email });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileDto dto, CancellationToken ct)
    {
        var uid = User.GetUserId();
        var name = Validation.Required(dto.FullName, "نام و نام خانوادگی", 100);
        var email = Validation.OptionalEmail(dto.Email);
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == uid, ct) ?? throw new DomainException("کاربر یافت نشد.", 404);
        var party = await db.Parties.FirstOrDefaultAsync(p => p.UserId == uid && p.IsCustomer, ct);
        u.FullName = name;
        if (party is not null) { party.Name = name; party.Email = email; }
        await db.SaveChangesAsync(ct);
        return Ok(new { fullName = u.FullName, phone = u.UserName, email });
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto, CancellationToken ct)
    {
        var uid = User.GetUserId();
        var newPassword = Validation.Password(dto.NewPassword);
        var u = await db.Users.FirstOrDefaultAsync(x => x.Id == uid, ct) ?? throw new DomainException("کاربر یافت نشد.", 404);
        if (hasher.VerifyHashedPassword(u, u.PasswordHash, dto.CurrentPassword ?? "") == PasswordVerificationResult.Failed)
            throw new DomainException("رمز عبور فعلی اشتباه است.");
        u.PasswordHash = hasher.HashPassword(u, newPassword);
        db.AuditLogs.Add(new AuditLog { UserId = uid, Action = "ChangePassword", Entity = "AppUser", EntityId = uid });
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    // ---- آدرس‌ها ----

    static object View(Address a) => new
    {
        a.Id, a.Title, a.RecipientName, a.Phone, a.Province, a.City, a.PostalCode, a.FullAddress, a.IsDefault
    };

    [HttpGet("addresses")]
    public async Task<IActionResult> Addresses(CancellationToken ct)
    {
        var uid = User.GetUserId();
        var list = await db.Addresses.AsNoTracking().Where(a => a.UserId == uid && a.IsActive)
            .OrderByDescending(a => a.IsDefault).ThenByDescending(a => a.Id).ToListAsync(ct);
        return Ok(list.Select(View));
    }

    static void Apply(Address a, AddressDto dto)
    {
        a.Title = Validation.Required(dto.Title, "عنوان آدرس", 50);
        a.RecipientName = Validation.Required(dto.RecipientName, "نام گیرنده", 100);
        a.Phone = Validation.NormalizePhone(dto.Phone);
        a.Province = Validation.Required(dto.Province, "استان", 50);
        a.City = Validation.Required(dto.City, "شهر", 50);
        a.PostalCode = Validation.PostalCode(dto.PostalCode);
        a.FullAddress = Validation.Required(dto.FullAddress, "نشانی کامل", 500);
    }

    async Task MakeDefaultAsync(int uid, Address target, CancellationToken ct)
    {
        var others = await db.Addresses.Where(a => a.UserId == uid && a.IsDefault && a.Id != target.Id).ToListAsync(ct);
        foreach (var o in others) o.IsDefault = false;
        target.IsDefault = true;
    }

    [HttpPost("addresses")]
    public async Task<IActionResult> CreateAddress(AddressDto dto, CancellationToken ct)
    {
        var uid = User.GetUserId();
        var a = new Address { UserId = uid };
        Apply(a, dto);
        db.Addresses.Add(a);
        // اولین آدرس کاربر خودکار پیش‌فرض می‌شود
        var first = !await db.Addresses.AnyAsync(x => x.UserId == uid && x.IsActive, ct);
        if (dto.IsDefault || first) await MakeDefaultAsync(uid, a, ct);
        await db.SaveChangesAsync(ct);
        return Ok(View(a));
    }

    [HttpPut("addresses/{id:int}")]
    public async Task<IActionResult> UpdateAddress(int id, AddressDto dto, CancellationToken ct)
    {
        var uid = User.GetUserId();
        var a = await db.Addresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid && x.IsActive, ct) ?? throw new DomainException("آدرس یافت نشد.", 404);
        Apply(a, dto);
        if (dto.IsDefault) await MakeDefaultAsync(uid, a, ct);
        await db.SaveChangesAsync(ct);
        return Ok(View(a));
    }

    [HttpPost("addresses/{id:int}/default")]
    public async Task<IActionResult> SetDefault(int id, CancellationToken ct)
    {
        var uid = User.GetUserId();
        var a = await db.Addresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid && x.IsActive, ct) ?? throw new DomainException("آدرس یافت نشد.", 404);
        await MakeDefaultAsync(uid, a, ct);
        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /// حذف منطقی (سفارش‌های قبلی اسنپ‌شات آدرس را نگه می‌دارند). اگر آدرس پیش‌فرض حذف شود، جدیدترین آدرس دیگر پیش‌فرض می‌شود.
    [HttpDelete("addresses/{id:int}")]
    public async Task<IActionResult> DeleteAddress(int id, CancellationToken ct)
    {
        var uid = User.GetUserId();
        var a = await db.Addresses.FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid && x.IsActive, ct) ?? throw new DomainException("آدرس یافت نشد.", 404);
        a.IsActive = false;
        if (a.IsDefault)
        {
            a.IsDefault = false;
            var next = await db.Addresses.Where(x => x.UserId == uid && x.IsActive && x.Id != id).OrderByDescending(x => x.Id).FirstOrDefaultAsync(ct);
            if (next is not null) next.IsDefault = true;
        }
        await db.SaveChangesAsync(ct);
        return NoContent();
    }
}
