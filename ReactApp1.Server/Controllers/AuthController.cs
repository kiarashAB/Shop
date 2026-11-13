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

        var identity = new ClaimsIdentity(
            [new(ClaimTypes.NameIdentifier, u.Id.ToString()), new(ClaimTypes.Name, u.UserName), new(ClaimTypes.Role, u.Role.ToString())],
            CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

        db.AuditLogs.Add(new AuditLog { UserId = u.Id, Action = "Login", Entity = "AppUser", EntityId = u.Id });
        await db.SaveChangesAsync(ct);
        return Ok(new { userName = u.UserName, fullName = u.FullName, role = u.Role.ToString() });
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
            .Select(x => new { x.UserName, x.FullName, Role = x.Role.ToString() }).FirstOrDefaultAsync(ct);
        return u is null ? Unauthorized() : Ok(u);
    }
}
