using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;
using ReactApp1.Server.Services;

namespace ReactApp1.Tests;

/// تست‌ها روی SQL Server واقعی (LocalDB) اجرا می‌شوند، نه InMemory، چون تراکنش‌ها و Decimal باید واقعی باشند.
/// برای سرور دیگر: متغیر محیطی TEST_SQL_SERVER را تنظیم کنید. هر تست یک دیتابیس موقت می‌سازد و حذف می‌کند.
public class AccountingFlowTests : IAsyncLifetime
{
    readonly string _dbName = $"RakhshanSamTests_{Guid.NewGuid():N}";
    AppDbContext db = null!;
    LedgerService ledger = null!;
    PaymentService pay = null!;
    PurchaseService purchases = null!;
    AppUser user = null!; Party supplier = null!; Product product = null!; FinancialAccount cash = null!; Warehouse wh = null!;

    public async Task InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable("TEST_SQL_SERVER") ?? @"(localdb)\MSSQLLocalDB";
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer($"Server={server};Database={_dbName};Trusted_Connection=True;TrustServerCertificate=True").Options;
        db = new AppDbContext(opts);
        await db.Database.EnsureCreatedAsync();
        await DbInitializer.SeedReferenceDataAsync(db);

        ledger = new LedgerService(db);
        pay = new PaymentService(db, ledger);
        purchases = new PurchaseService(db, ledger, pay, new InventoryService(db, ledger));

        user = new AppUser { UserName = "tester", FullName = "Tester", PasswordHash = "x", Role = UserRole.SuperAdmin };
        supplier = new Party { Name = "تأمین‌کننده نمونه", IsSupplier = true };
        var cat = await db.Categories.FirstAsync();
        product = new Product { Name = "برد سام", Slug = "board-1", Sku = "B-1", CategoryId = cat.Id, SalePrice = 1_500_000_000m };
        db.AddRange(user, supplier, product);
        await db.SaveChangesAsync();
        cash = await db.FinancialAccounts.FirstAsync(a => a.Kind == AccountKind.Cash);
        wh = await db.Warehouses.FirstAsync();
    }

    public async Task DisposeAsync()
    {
        await db.Database.EnsureDeletedAsync();
        await db.DisposeAsync();
    }

    // مثال پروژه: خرید ۱۰۰ میلیون تومان (= ۱ میلیارد ریال)، پرداخت ۳۰ میلیون تومان (= ۳۰۰ میلیون ریال)
    CreatePurchaseDto Example(string key) => new(supplier.Id, wh.Id, DateTime.UtcNow, "INV-1", null,
        [new PurchaseItemDto(product.Id, 10, 100_000_000m)], 300_000_000m, PaymentMethod.Cash, cash.Id, null, key);

    [Fact]
    public async Task Purchase_100M_toman_with_30M_paid_leaves_70M_debt()
    {
        var draft = await purchases.CreateDraftAsync(Example("k1"), user.Id, default);
        await purchases.ConfirmAsync(draft.Id, user.Id, default);

        var p = await db.Products.AsNoTracking().SingleAsync();
        Assert.Equal(10, p.StockQuantity);
        Assert.Equal(100_000_000m, p.AverageCost);

        var ledgerResult = await ledger.GetPartyLedgerAsync(supplier.Id, null, null, default);
        Assert.Equal(-700_000_000m, ledgerResult.Balance);        // ۷۰ میلیون تومان بدهی
        Assert.Equal(2, ledgerResult.Rows.Count);                 // یک خرید + یک پرداخت

        var tb = await ledger.GetTrialBalanceAsync(default);
        Assert.True(tb.IsBalanced);
        Assert.Equal(300_000_000m, tb.Rows.Single(r => r.Code == "1010").Credit);   // خروج از صندوق
        Assert.Equal(1_000_000_000m, tb.Rows.Single(r => r.Code == "1100").Debit);  // ورود به موجودی

        var kardex = await db.InventoryTransactions.AsNoTracking().ToListAsync();
        Assert.Single(kardex);
        Assert.Equal(10, kardex[0].BalanceAfter);
    }

    [Fact]
    public async Task Confirming_twice_does_not_duplicate_anything()
    {
        var draft = await purchases.CreateDraftAsync(Example("k2"), user.Id, default);
        await purchases.ConfirmAsync(draft.Id, user.Id, default);
        await purchases.ConfirmAsync(draft.Id, user.Id, default);

        Assert.Equal(10, (await db.Products.AsNoTracking().SingleAsync()).StockQuantity);
        Assert.Equal(2, await db.FinancialDocuments.CountAsync());   // خرید + پرداخت
        Assert.Equal(1, await db.Payments.CountAsync());
        Assert.Equal(1, await db.InventoryTransactions.CountAsync());
    }

    [Fact]
    public async Task Same_idempotency_key_returns_same_draft()
    {
        var a = await purchases.CreateDraftAsync(Example("k3"), user.Id, default);
        var b = await purchases.CreateDraftAsync(Example("k3"), user.Id, default);
        Assert.Equal(a.Id, b.Id);
        Assert.Equal(1, await db.PurchaseReceipts.CountAsync());
    }

    [Fact]
    public async Task Payment_larger_than_remaining_is_rejected()
    {
        var draft = await purchases.CreateDraftAsync(Example("k4"), user.Id, default);
        await purchases.ConfirmAsync(draft.Id, user.Id, default);

        await Assert.ThrowsAsync<DomainException>(() => pay.RegisterAsync(
            new PaymentRequest(PaymentDirection.ToSupplier, supplier.Id, 700_000_001m, PaymentMethod.Cash, cash.Id, null, DateTime.UtcNow, draft.Id, "p-over", null), user.Id, default));

        await pay.RegisterAsync(
            new PaymentRequest(PaymentDirection.ToSupplier, supplier.Id, 700_000_000m, PaymentMethod.Cash, cash.Id, null, DateTime.UtcNow, draft.Id, "p-ok", null), user.Id, default);
        Assert.Equal(0m, (await ledger.GetPartyLedgerAsync(supplier.Id, null, null, default)).Balance);
    }

    [Fact]
    public async Task Same_payment_key_is_not_posted_twice()
    {
        var draft = await purchases.CreateDraftAsync(Example("k5"), user.Id, default);
        await purchases.ConfirmAsync(draft.Id, user.Id, default);
        var req = new PaymentRequest(PaymentDirection.ToSupplier, supplier.Id, 100_000_000m, PaymentMethod.Cash, cash.Id, null, DateTime.UtcNow, draft.Id, "p-dup", null);
        var a = await pay.RegisterAsync(req, user.Id, default);
        var b = await pay.RegisterAsync(req, user.Id, default);
        Assert.Equal(a.Id, b.Id);
        Assert.Equal(-600_000_000m, (await ledger.GetPartyLedgerAsync(supplier.Id, null, null, default)).Balance);
    }

    [Fact]
    public async Task Reversal_after_stock_is_used_is_blocked_and_otherwise_zeroes_inventory_and_keeps_books_balanced()
    {
        var draft = await purchases.CreateDraftAsync(Example("k6"), user.Id, default);
        await purchases.ConfirmAsync(draft.Id, user.Id, default);

        // مصرف ۳ عدد → برگشت کامل رسید دیگر ممکن نیست
        var inv = new InventoryService(db, ledger);
        await inv.IssueAsync(new IssueStockDto(product.Id, wh.Id, 3, StockReason.Damage, null, null), user.Id, default);
        await Assert.ThrowsAsync<DomainException>(() => purchases.ReverseAsync(draft.Id, "اشتباه ثبت شد", user.Id, default));
        db.ChangeTracker.Clear();
        Assert.Equal(7, (await db.Products.AsNoTracking().SingleAsync()).StockQuantity);
        Assert.True((await ledger.GetTrialBalanceAsync(default)).IsBalanced);
    }
}
