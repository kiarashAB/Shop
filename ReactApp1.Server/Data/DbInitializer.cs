using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Domain;

namespace ReactApp1.Server.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider sp, IConfiguration cfg, bool migrate)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
        if (migrate)
        {
            // اگر Migration ساخته شده باشد همان اعمال می‌شود؛ در غیر این صورت (توسعهٔ محلی) دیتابیس و جدول‌ها از روی مدل ساخته می‌شوند.
            if (db.Database.GetMigrations().Any()) await db.Database.MigrateAsync();
            else await db.Database.EnsureCreatedAsync();
        }

        await SeedReferenceDataAsync(db);

        if (!await db.Users.AnyAsync())
        {
            var pwd = cfg["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(pwd) || pwd.Length < 10)
            {
                log.LogWarning("کاربر مدیر ساخته نشد: Seed:AdminPassword (حداقل ۱۰ کاراکتر) تنظیم نشده است.");
                return;
            }
            var hasher = sp.GetRequiredService<IPasswordHasher<AppUser>>();
            var admin = new AppUser { UserName = cfg["Seed:AdminUserName"] ?? "admin", FullName = "مدیر ارشد", Role = UserRole.SuperAdmin };
            admin.PasswordHash = hasher.HashPassword(admin, pwd);
            db.Users.Add(admin);
            await db.SaveChangesAsync();
        }
    }

    /// داده‌های پایهٔ بدون وابستگی به محیط (در تست‌ها هم استفاده می‌شود). فقط اگر وجود نداشته باشند ساخته می‌شوند.
    public static async Task SeedReferenceDataAsync(AppDbContext db)
    {
        if (!await db.FinancialAccounts.AnyAsync())
        {
            db.FinancialAccounts.AddRange(
                new FinancialAccount { Code = "1010", Name = "صندوق", Kind = AccountKind.Cash },
                new FinancialAccount { Code = "1020", Name = "بانک", Kind = AccountKind.Bank },
                new FinancialAccount { Code = "1100", Name = "موجودی کالا", Kind = AccountKind.Inventory, IsSystem = true },
                new FinancialAccount { Code = "1200", Name = "حساب‌های دریافتنی (مشتریان)", Kind = AccountKind.Receivable, IsSystem = true },
                new FinancialAccount { Code = "2100", Name = "حساب‌های پرداختنی (تأمین‌کنندگان)", Kind = AccountKind.Payable, IsSystem = true },
                new FinancialAccount { Code = "4100", Name = "فروش", Kind = AccountKind.Sales, IsSystem = true },
                new FinancialAccount { Code = "5100", Name = "بهای تمام‌شدهٔ کالای فروخته‌شده", Kind = AccountKind.CostOfGoods, IsSystem = true },
                new FinancialAccount { Code = "6100", Name = "ضایعات و مصرف داخلی", Kind = AccountKind.OtherExpense, IsSystem = true });
        }
        if (!await db.Warehouses.AnyAsync()) db.Warehouses.Add(new Warehouse { Name = "انبار اصلی" });

        foreach (var key in new[] { "FD", "PAY", "PR", "WO" })
            if (!await db.DocumentCounters.AnyAsync(c => c.Key == key))
                db.DocumentCounters.Add(new DocumentCounter { Key = key });

        if (!await db.Categories.AnyAsync())
        {
            string[][] cats =
            [
                ["آرایش صورت", "face-makeup"], ["آرایش چشم", "eye-makeup"], ["آرایش لب", "lip-makeup"],
                ["مراقبت پوست", "skincare"], ["مراقبت مو", "haircare"], ["عطر و ادکلن", "perfume"],
                ["بهداشت و زیبایی", "hygiene-beauty"], ["ابزار و لوازم آرایشی", "makeup-tools"],
                ["محصولات آقایان", "men-grooming"], ["سایر محصولات", "other-products"]
            ];
            for (var i = 0; i < cats.Length; i++)
                db.Categories.Add(new Category { Name = cats[i][0], Slug = cats[i][1], SortOrder = i });
        }
        await db.SaveChangesAsync();
    }
}
