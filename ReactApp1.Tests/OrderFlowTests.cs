using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;
using ReactApp1.Server.Services;

namespace ReactApp1.Tests;

/// تست‌های چرخهٔ سفارش روی SQL Server واقعی (LocalDB/SQLEXPRESS)، مثل AccountingFlowTests.
/// برای سرور دیگر: متغیر محیطی TEST_SQL_SERVER را تنظیم کنید. هر تست یک دیتابیس موقت می‌سازد و حذف می‌کند.
public class OrderFlowTests : IAsyncLifetime
{
    readonly string _dbName = $"GolrokhOrderTests_{Guid.NewGuid():N}";
    AppDbContext db = null!;
    LedgerService ledger = null!;
    CartService cart = null!;
    OrderService orders = null!;
    AppUser admin = null!, customerUser = null!;
    Party customer = null!;
    Product product = null!;
    Address address = null!;
    ShippingMethod shipping = null!;
    FinancialAccount cash = null!;

    const decimal Price = 1_500_000_000m;      // ریال
    const decimal Cost = 100_000_000m;         // بهای خرید هر واحد (ریال)
    const decimal ShippingPrice = 200_000m;

    public async Task InitializeAsync()
    {
        var server = Environment.GetEnvironmentVariable("TEST_SQL_SERVER") ?? @"(localdb)\MSSQLLocalDB";
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer($"Server={server};Database={_dbName};Trusted_Connection=True;TrustServerCertificate=True").Options;
        db = new AppDbContext(opts);
        await db.Database.EnsureCreatedAsync();
        await DbInitializer.SeedReferenceDataAsync(db);

        ledger = new LedgerService(db);
        var inventory = new InventoryService(db, ledger);
        var pay = new PaymentService(db, ledger);
        var purchases = new PurchaseService(db, ledger, pay, inventory);
        cart = new CartService(db);
        orders = new OrderService(db, ledger, inventory, pay, cart);

        admin = new AppUser { UserName = "tester", FullName = "Tester", PasswordHash = "x", Role = UserRole.SuperAdmin };
        customerUser = new AppUser { UserName = "09120000000", FullName = "Ali", PasswordHash = "x", Role = UserRole.Customer };
        var supplier = new Party { Name = "تأمین‌کننده نمونه", IsSupplier = true };
        var cat = await db.Categories.FirstAsync();
        product = new Product { Name = "رژ لب", Slug = "lipstick-1", Sku = "L-1", CategoryId = cat.Id, SalePrice = Price, IsPublished = true };
        db.AddRange(admin, customerUser, supplier, product);
        await db.SaveChangesAsync();

        customer = new Party { Name = "Ali", Phone = "09120000000", IsCustomer = true, UserId = customerUser.Id };
        address = new Address
        {
            UserId = customerUser.Id, Title = "خانه", RecipientName = "Ali", Phone = "09120000000",
            Province = "تهران", City = "تهران", FullAddress = "خیابان نمونه، پلاک ۱", IsDefault = true
        };
        shipping = await db.ShippingMethods.FirstAsync();
        shipping.Price = ShippingPrice;
        db.AddRange(customer, address);
        await db.SaveChangesAsync();

        cash = await db.FinancialAccounts.FirstAsync(a => a.Kind == AccountKind.Cash);
        var wh = await db.Warehouses.FirstAsync();

        // موجودی اولیه: ۱۰ عدد با بهای خرید ۱۰۰ میلیون ریال، از مسیر رسید خرید (تا حسابداری هم تراز باشد)
        var draft = await purchases.CreateDraftAsync(
            new CreatePurchaseDto(supplier.Id, wh.Id, DateTime.UtcNow, "INV-1", null, [new PurchaseItemDto(product.Id, 10, Cost)]),
            admin.Id, default);
        await purchases.ConfirmAsync(draft.Id, admin.Id, default);
    }

    public async Task DisposeAsync()
    {
        await db.Database.EnsureDeletedAsync();
        await db.DisposeAsync();
    }

    Task<Order> CreateOrder(string key, int qty, string? code = null) =>
        orders.CreateAsync(customerUser.Id,
            new CreateOrderDto(address.Id, shipping.Id, code, null, [new CartItemDto(product.Id, qty)], key), default);

    Task<Product> Reload() => db.Products.AsNoTracking().SingleAsync();

    [Fact]
    public async Task Order_deducts_stock_posts_balanced_sale_and_debits_customer()
    {
        var o = await CreateOrder("o1", 3);

        Assert.Equal(3 * Price + ShippingPrice, o.Total);
        Assert.Equal(OrderStatus.PendingPayment, o.Status);
        Assert.StartsWith("OR-", o.Number);

        var p = await Reload();
        Assert.Equal(7, p.StockQuantity);

        var tb = await ledger.GetTrialBalanceAsync(default);
        Assert.True(tb.IsBalanced);
        Assert.Equal(o.Total, tb.Rows.Single(r => r.Code == "4100").Credit);            // فروش
        Assert.Equal(3 * Cost, tb.Rows.Single(r => r.Code == "5100").Debit);            // بهای تمام‌شدهٔ کالای فروخته‌شده

        var ledgerResult = await ledger.GetPartyLedgerAsync(customer.Id, null, null, default);
        Assert.Equal(o.Total, ledgerResult.Balance);                                    // مثبت = مشتری به ما بدهکار است

        var sale = await db.InventoryTransactions.AsNoTracking().SingleAsync(t => t.Reason == StockReason.Sale);
        Assert.Equal(-3, sale.QuantityChange);
        Assert.Equal(o.Number, sale.DocumentNumber);
        Assert.Equal(7, sale.BalanceAfter);
    }

    [Fact]
    public async Task Same_idempotency_key_creates_one_order_and_deducts_once()
    {
        var a = await CreateOrder("o2", 3);
        var b = await CreateOrder("o2", 3);

        Assert.Equal(a.Id, b.Id);
        Assert.Equal(1, await db.Orders.CountAsync());
        Assert.Equal(7, (await Reload()).StockQuantity);
    }

    [Fact]
    public async Task Order_larger_than_stock_is_rejected_and_nothing_is_written()
    {
        await Assert.ThrowsAsync<DomainException>(() => CreateOrder("o3", 11));

        Assert.Equal(0, await db.Orders.AsNoTracking().CountAsync());
        Assert.Equal(10, (await Reload()).StockQuantity);
        Assert.Equal(0, await db.InventoryTransactions.AsNoTracking().CountAsync(t => t.Reason == StockReason.Sale));
        Assert.True((await ledger.GetTrialBalanceAsync(default)).IsBalanced);
    }

    [Fact]
    public async Task Cancel_restores_stock_reverses_sale_and_is_idempotent()
    {
        var o = await CreateOrder("o4", 3);
        await orders.CancelAsync(o.Id, "انصراف مشتری", admin.Id, byCustomer: false, default);
        await orders.CancelAsync(o.Id, "انصراف مشتری", admin.Id, byCustomer: false, default);

        Assert.Equal(10, (await Reload()).StockQuantity);
        Assert.Equal(OrderStatus.Cancelled, (await db.Orders.AsNoTracking().SingleAsync()).Status);
        Assert.True((await ledger.GetTrialBalanceAsync(default)).IsBalanced);
        Assert.Equal(0m, (await ledger.GetPartyLedgerAsync(customer.Id, null, null, default)).Balance);
        Assert.Equal(1, await db.InventoryTransactions.AsNoTracking().CountAsync(t => t.Reason == StockReason.Return));
    }

    [Fact]
    public async Task Payments_settle_the_order_overpay_is_rejected_and_same_key_is_not_posted_twice()
    {
        var o = await CreateOrder("o5", 1);
        var half = o.Total / 2;

        await orders.RegisterPaymentAsync(o.Id, new OrderPaymentDto(half, PaymentMethod.Cash, cash.Id, null, null, "pay-1", null), admin.Id, default);
        await orders.RegisterPaymentAsync(o.Id, new OrderPaymentDto(half, PaymentMethod.Cash, cash.Id, null, null, "pay-1", null), admin.Id, default);
        var afterHalf = await db.Orders.AsNoTracking().SingleAsync();
        Assert.Equal(half, afterHalf.PaidAmount);
        Assert.Equal(OrderStatus.PendingPayment, afterHalf.Status);
        Assert.Equal(1, await db.Payments.AsNoTracking().CountAsync());

        var remaining = o.Total - half;
        await Assert.ThrowsAsync<DomainException>(() => orders.RegisterPaymentAsync(o.Id,
            new OrderPaymentDto(remaining + 1, PaymentMethod.Cash, cash.Id, null, null, "pay-over", null), admin.Id, default));

        await orders.RegisterPaymentAsync(o.Id, new OrderPaymentDto(remaining, PaymentMethod.Cash, cash.Id, null, null, "pay-2", null), admin.Id, default);
        var settled = await db.Orders.AsNoTracking().SingleAsync();
        Assert.Equal(settled.Total, settled.PaidAmount);
        Assert.Equal(OrderStatus.Processing, settled.Status);
        Assert.Equal(0m, (await ledger.GetPartyLedgerAsync(customer.Id, null, null, default)).Balance);
        Assert.True((await ledger.GetTrialBalanceAsync(default)).IsBalanced);
    }

    [Fact]
    public async Task Percent_discount_applies_and_max_uses_is_enforced_and_released_on_cancel()
    {
        db.Discounts.Add(new Discount { Code = "OFF10", Type = DiscountType.Percent, Value = 10, MaxUses = 1 });
        await db.SaveChangesAsync();

        var first = await CreateOrder("d1", 1, "off10");
        Assert.Equal(Price / 10, first.DiscountAmount);
        Assert.Equal(Price - Price / 10 + ShippingPrice, first.Total);
        Assert.Equal("OFF10", first.DiscountCode);

        await Assert.ThrowsAsync<DomainException>(() => CreateOrder("d2", 1, "OFF10"));
        Assert.Equal(1, await db.Orders.AsNoTracking().CountAsync());

        await orders.CancelAsync(first.Id, "تست", admin.Id, byCustomer: false, default);
        Assert.Equal(0, (await db.Discounts.AsNoTracking().SingleAsync()).UsedCount);
        var again = await CreateOrder("d3", 1, "OFF10");
        Assert.Equal("OFF10", again.DiscountCode);
    }

    [Fact]
    public async Task Shipment_flow_moves_to_shipped_counts_sales_and_blocks_cancel_after_shipping()
    {
        var o = await CreateOrder("s1", 2);

        await Assert.ThrowsAsync<DomainException>(() => orders.ChangeStatusAsync(o.Id, OrderStatus.Shipped, null, admin.Id, default));
        await Assert.ThrowsAsync<DomainException>(() => orders.SetShipmentAsync(o.Id, new ShipmentDto("پست", "123456", null), admin.Id, default)); // هنوز پردازش نشده

        await orders.ChangeStatusAsync(o.Id, OrderStatus.Processing, "تأیید تلفنی", admin.Id, default);
        await orders.SetShipmentAsync(o.Id, new ShipmentDto("پست", "123456", null), admin.Id, default);

        var shipped = await db.Orders.AsNoTracking().Include(x => x.Shipment).SingleAsync();
        Assert.Equal(OrderStatus.Shipped, shipped.Status);
        Assert.Equal("123456", shipped.Shipment!.TrackingCode);
        Assert.Equal(2, (await Reload()).SoldCount);

        await Assert.ThrowsAsync<DomainException>(() => orders.CancelAsync(o.Id, "دیر شده", admin.Id, byCustomer: false, default));
        await orders.ChangeStatusAsync(o.Id, OrderStatus.Delivered, null, admin.Id, default);
        Assert.Equal(OrderStatus.Delivered, (await db.Orders.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task Quote_uses_server_prices_flags_insufficient_stock_and_reports_bad_discount_codes()
    {
        var ok = await cart.QuoteAsync(new QuoteRequest([new CartItemDto(product.Id, 2)], null, shipping.Id), default);
        Assert.True(ok.CanCheckout);
        Assert.Equal(2 * Price, ok.Subtotal);
        Assert.Equal(2 * Price + ShippingPrice, ok.Total);

        var tooMany = await cart.QuoteAsync(new QuoteRequest([new CartItemDto(product.Id, 11)], null, null), default);
        Assert.False(tooMany.CanCheckout);
        Assert.NotNull(tooMany.Lines.Single().Problem);

        var bad = await cart.QuoteAsync(new QuoteRequest([new CartItemDto(product.Id, 1)], "NOPE", null), default);
        Assert.NotNull(bad.DiscountError);
        Assert.Equal(0m, bad.DiscountAmount);
    }
}
