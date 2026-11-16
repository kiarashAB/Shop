using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
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
