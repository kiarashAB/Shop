using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;
using ReactApp1.Server.Infrastructure;
using ReactApp1.Server.Services;

namespace ReactApp1.Server.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(AppDbContext db, IPasswordHasher<AppUser> hasher) : ControllerBase
{
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginDto dto, CancellationToken ct)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.UserName == dto.UserName && x.IsActive, ct);
        if (u is null || hasher.VerifyHashedPassword(u, u.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { title = "نام کاربری یا رمز عبور اشتباه است." });

        await SignInAsync(u);

        db.AuditLogs.Add(new AuditLog { UserId = u.Id, Action = "Login", Entity = "AppUser", EntityId = u.Id });
        await db.SaveChangesAsync(ct);
        return Ok(new { userName = u.UserName, fullName = u.FullName, role = u.Role.ToString() });
    }

    /// ثبت‌نام مشتری: حساب کاربری (نقش Customer) + پروفایل مشتری در دفتر طرف‌حساب‌ها. نام کاربری همان شمارهٔ موبایل است.
    [HttpPost("register"), AllowAnonymous, EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(RegisterDto dto, CancellationToken ct)
    {
        var phone = Validation.NormalizePhone(dto.Phone);
        var name = Validation.Required(dto.FullName, "نام و نام خانوادگی", 100);
        var email = Validation.OptionalEmail(dto.Email);
        var password = Validation.Password(dto.Password);
        if (await db.Users.AnyAsync(x => x.UserName == phone, ct))
            throw new DomainException("با این شماره قبلاً ثبت‌نام شده است. وارد شوید.", 409);

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = new AppUser { UserName = phone, FullName = name, Role = UserRole.Customer };
        user.PasswordHash = hasher.HashPassword(user, password);
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        // اگر مدیر قبلاً این شماره را به‌عنوان مشتری (بدون حساب) ثبت کرده، همان پروفایل و سوابقش به حساب جدید وصل می‌شود.
        var party = await db.Parties.FirstOrDefaultAsync(p => p.Phone == phone && p.IsCustomer && p.UserId == null, ct);
        if (party is null)
        {
            party = new Party { Name = name, Phone = phone, Email = email, IsCustomer = true };
            db.Parties.Add(party);
        }
        party.UserId = user.Id;
        db.AuditLogs.Add(new AuditLog { UserId = user.Id, Action = "Register", Entity = "AppUser", EntityId = user.Id });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        await SignInAsync(user);
        return Ok(new { userName = user.UserName, fullName = user.FullName, role = user.Role.ToString() });
    }

    Task SignInAsync(AppUser u)
    {
        var identity = new ClaimsIdentity(
            [new(ClaimTypes.NameIdentifier, u.Id.ToString()), new(ClaimTypes.Name, u.UserName), new(ClaimTypes.Role, u.Role.ToString())],
            CookieAuthenticationDefaults.AuthenticationScheme);
        return HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));
    }

    [HttpPost("logout"), Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }

    [HttpGet("me"), Authorize]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var id = User.GetUserId();
        var u = await db.Users.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new { x.UserName, x.FullName, x.Role }).FirstOrDefaultAsync(ct);
        return u is null ? Unauthorized() : Ok(u);
    }
}
