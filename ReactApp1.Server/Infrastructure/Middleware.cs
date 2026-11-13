namespace ReactApp1.Server.Infrastructure;

/// هدرهای امنیتی پایه روی همهٔ پاسخ‌ها.
public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext ctx)
    {
        ctx.Response.OnStarting(() =>
        {
            var h = ctx.Response.Headers;
            h["X-Content-Type-Options"] = "nosniff";
            h["X-Frame-Options"] = "DENY";
            h["Referrer-Policy"] = "strict-origin-when-cross-origin";
            h["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            return Task.CompletedTask;
        });
        return next(ctx);
    }
}

/// دفاع در عمق در برابر CSRF: درخواست تغییردهنده‌ای که هدر Origin دارد، فقط اگر هم‌مبدأ یا در فهرست مجاز باشد پذیرفته می‌شود.
public class OriginCheckMiddleware(RequestDelegate next, string[] allowedOrigins)
{
    static readonly HashSet<string> Safe = new(StringComparer.OrdinalIgnoreCase) { "GET", "HEAD", "OPTIONS", "TRACE" };

    public async Task InvokeAsync(HttpContext ctx)
    {
        if (!Safe.Contains(ctx.Request.Method) && ctx.Request.Headers.TryGetValue("Origin", out var origin) && origin.Count > 0)
        {
            var o = origin.ToString().TrimEnd('/');
            var sameOrigin = string.Equals(o, $"{ctx.Request.Scheme}://{ctx.Request.Host}", StringComparison.OrdinalIgnoreCase);
            var allowed = allowedOrigins.Any(a => string.Equals(a.TrimEnd('/'), o, StringComparison.OrdinalIgnoreCase));
            if (!sameOrigin && !allowed)
            {
                ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
                await ctx.Response.WriteAsJsonAsync(new { message = "مبدأ درخواست مجاز نیست." });
                return;
            }
        }
        await next(ctx);
    }
}
