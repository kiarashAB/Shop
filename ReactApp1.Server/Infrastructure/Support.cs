using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ReactApp1.Server.Domain;

namespace ReactApp1.Server.Infrastructure;

public static class AppRoles
{
    public const string Finance = "SuperAdmin,Admin,Accountant";
    public const string Stock = "SuperAdmin,Admin,InventoryManager";
    public const string StockAndFinance = "SuperAdmin,Admin,InventoryManager,Accountant";
    public const string Catalog = "SuperAdmin,Admin,ProductManager";
    public const string People = "SuperAdmin,Admin,Accountant,OrderManager,Support,InventoryManager";
    public const string Content = "SuperAdmin,Admin,ContentManager";
}

public static class UserExtensions
{
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new UnauthorizedAccessException());
}

public class JwtService(IConfiguration cfg)
{
    public string Create(AppUser u)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: cfg["Jwt:Issuer"] ?? "rakhshansam", audience: cfg["Jwt:Audience"] ?? "rakhshansam",
            claims: [new(ClaimTypes.NameIdentifier, u.Id.ToString()), new(ClaimTypes.Name, u.UserName), new(ClaimTypes.Role, u.Role.ToString())],
            expires: DateTime.UtcNow.AddHours(8), signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

public class ApiExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext ctx, Exception ex, CancellationToken ct)
    {
        (int status, string title) = ex switch
        {
            DomainException d => (d.StatusCode, d.Message),
            DbUpdateConcurrencyException => (409, "این اطلاعات هم‌زمان توسط عملیات دیگری تغییر کرد. دوباره تلاش کنید."),
            DbUpdateException => (409, "ثبت انجام نشد: داده تکراری یا ناسازگار است."),
            _ => (0, "")
        };
        if (status == 0) return false;
        ctx.Response.StatusCode = status;
        return await problems.TryWriteAsync(new ProblemDetailsContext { HttpContext = ctx, ProblemDetails = { Status = status, Title = title } });
    }
}
