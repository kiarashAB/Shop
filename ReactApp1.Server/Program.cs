using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;
using ReactApp1.Server.Infrastructure;
using ReactApp1.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// حجم بدنه‌ی درخواست: پیش‌فرض ۱ مگابایت.
builder.WebHost.ConfigureKestrel(options =>
{
    options.AddServerHeader = false;
    options.Limits.MaxRequestBodySize = 1 * 1024 * 1024;
});

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default تنظیم نشده است (appsettings.Development.json یا user-secrets).");

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(connectionString));

// =========================
// CORS
// فرانت‌اند با کوکی (credentials) به API وصل می‌شود؛ پس Origin باید مشخص باشد.
// برای هاست، آدرس سایت را در Cors:AllowedOrigins تنظیم کنید.
// =========================

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();

if (allowedOrigins == null || allowedOrigins.Length == 0)
{
    allowedOrigins =
    [
        "https://localhost:22324",
        "http://localhost:22324",
        "https://localhost:5173",
        "http://localhost:5173"
    ];
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

// =========================
// Authentication (کوکی HttpOnly)
// نشست در مرورگر با JavaScript قابل خواندن نیست و توکنی در sessionStorage/localStorage نگه‌داری نمی‌شود.
// =========================

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "Shop.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;

        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;

        // برای API به‌جای ریدایرکت، کد وضعیت برمی‌گردد
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };

        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };

        // در هر درخواست، وضعیت فعال بودن و نقش کاربر با دیتابیس چک می‌شود؛
        // پس غیرفعال کردن حساب یا تغییر نقش بلافاصله اثر می‌کند.
        options.Events.OnValidatePrincipal = async context =>
        {
            var userIdText = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(userIdText, out var userId))
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();

            var account = await db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.IsActive, u.Role })
                .FirstOrDefaultAsync();

            var cookieRole = context.Principal?.FindFirst(ClaimTypes.Role)?.Value;

            if (account == null || !account.IsActive || account.Role.ToString() != cookieRole)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            }
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddScoped<LedgerService>();
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddScoped<PurchaseService>();
builder.Services.AddSingleton<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

// =========================
// پشت Reverse Proxy (IIS/Nginx/Cloudflare): آدرس واقعی کاربر برای Rate Limiting و Audit Log.
// پیش‌فرض خاموش است (هدرهای ساختگی کلاینت نباید اعتبار بگیرند).
// فعال‌سازی: ForwardedHeaders:Enabled=true و فهرست ForwardedHeaders:KnownProxies (IP پروکسی).
// =========================
var forwardedEnabled = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled");

if (forwardedEnabled)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

        var proxies = builder.Configuration
            .GetSection("ForwardedHeaders:KnownProxies")
            .Get<string[]>() ?? [];

        foreach (var proxy in proxies)
        {
            if (System.Net.IPAddress.TryParse(proxy, out var address))
                options.KnownProxies.Add(address);
        }
    });
}

// =========================
// Rate Limiting (جلوگیری از حدس رمز، اسکرپ و سوءاستفاده)
// کلید محدودیت: کاربر واردشده یا آدرس IP.
// =========================

static string RateKey(HttpContext http) =>
    http.User.Identity?.IsAuthenticated == true
        ? "u:" + http.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        : "ip:" + (http.Connection.RemoteIpAddress?.ToString() ?? "unknown");

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { message = "تعداد درخواست‌ها زیاد است. کمی بعد دوباره تلاش کنید." }, token);
    };

    // ورود
    options.AddPolicy("auth", http =>
        RateLimitPartition.GetFixedWindowLimiter(
            "ip:" + (http.Connection.RemoteIpAddress?.ToString() ?? "unknown"),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    // جستجو (هر بار تایپ یک درخواست است)
    options.AddPolicy("search", http =>
        RateLimitPartition.GetFixedWindowLimiter(
            RateKey(http),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 90, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));

    // سقف کلی برای هر کاربر/IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        http => RateLimitPartition.GetFixedWindowLimiter(
            RateKey(http),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 600, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

builder.Services.AddControllers().AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();

var app = builder.Build();

app.UseDefaultFiles();
app.MapStaticAssets();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    // در Production جزئیات خطا و Stack Trace هرگز به کاربر نمایش داده نمی‌شود
    app.UseHsts();
}

// DomainException و خطاهای هم‌زمانی به کد وضعیت مناسب تبدیل می‌شوند؛ بقیه خطاها 500 بدون جزئیات.
app.UseExceptionHandler();

if (forwardedEnabled)
    app.UseForwardedHeaders();

app.UseHttpsRedirection();

app.UseMiddleware<SecurityHeadersMiddleware>();

// فعال کردن CORS
app.UseCors("Frontend");

// دفاع در عمق در برابر CSRF: درخواست‌های تغییردهنده با Origin ناشناخته رد می‌شوند
app.UseMiddleware<OriginCheckMiddleware>((object)allowedOrigins);

app.UseAuthentication();

// بعد از احراز هویت تا محدودیت برای کاربر واردشده بر اساس شناسه‌اش باشد
app.UseRateLimiter();

app.UseAuthorization();

app.MapControllers();
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("/index.html");

using (var scope = app.Services.CreateScope())
    await DbInitializer.InitializeAsync(scope.ServiceProvider, app.Configuration, migrate: app.Environment.IsDevelopment());

app.Run();

public partial class Program;
