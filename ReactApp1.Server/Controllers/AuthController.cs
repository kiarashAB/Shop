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
public class AuthController(AppDbContext db, IPasswordHasher<AppUser> hasher, JwtService jwt) : ControllerBase
{
    [HttpPost("login"), AllowAnonymous, EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginDto dto, CancellationToken ct)
    {
        var u = await db.Users.FirstOrDefaultAsync(x => x.UserName == dto.UserName && x.IsActive, ct);
        if (u is null || hasher.VerifyHashedPassword(u, u.PasswordHash, dto.Password) == PasswordVerificationResult.Failed)
            return Unauthorized(new { title = "نام کاربری یا رمز عبور اشتباه است." });
        db.AuditLogs.Add(new AuditLog { UserId = u.Id, Action = "Login", Entity = "AppUser", EntityId = u.Id });
        await db.SaveChangesAsync(ct);
        return Ok(new { token = jwt.Create(u), userName = u.UserName, fullName = u.FullName, role = u.Role.ToString() });
    }
}
