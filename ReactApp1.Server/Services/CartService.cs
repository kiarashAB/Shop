using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;

namespace ReactApp1.Server.Services;

/// قیمت‌گذاری سبد و سبد ذخیره‌شدهٔ کاربر. قیمت و موجودی همیشه از دیتابیس خوانده می‌شود، نه از فرانت‌اند.
public class CartService(AppDbContext db)
{
    public const int MaxLineQuantity = 999;

    public static decimal FinalUnitPrice(decimal salePrice, decimal discountPercent) =>
        Math.Round(salePrice * (1 - discountPercent / 100m), 0, MidpointRounding.AwayFromZero);

    /// ردیف‌های تکراری را ادغام می‌کند و تعداد نامعتبر را رد می‌کند.
    public static List<CartItemDto> Normalize(IEnumerable<CartItemDto>? items)
    {
        if (items is null) return [];
        var merged = new Dictionary<int, int>();
        foreach (var i in items)
        {
            if (i.Quantity < 1 || i.Quantity > MaxLineQuantity)
                throw new DomainException($"تعداد هر محصول باید بین ۱ تا {MaxLineQuantity} باشد.");
            merged[i.ProductId] = merged.GetValueOrDefault(i.ProductId) + i.Quantity;
        }
        return merged.Select(kv => new CartItemDto(kv.Key, Math.Min(kv.Value, MaxLineQuantity))).ToList();
    }

    public async Task<(Discount? Discount, decimal Amount, string? Error)> EvaluateDiscountAsync(string? code, decimal subtotal, CancellationToken ct)
    {
        var norm = code?.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(norm)) return (null, 0, null);

        var d = await db.Discounts.AsNoTracking().FirstOrDefaultAsync(x => x.Code == norm, ct);
        var now = DateTime.UtcNow;
        if (d is null || !d.IsActive) return (null, 0, "کد تخفیف معتبر نیست.");
        if (d.StartsAt is not null && d.StartsAt > now) return (null, 0, "زمان استفاده از این کد هنوز شروع نشده است.");
        if (d.EndsAt is not null && d.EndsAt < now) return (null, 0, "مهلت استفاده از این کد تمام شده است.");
        if (d.MaxUses is not null && d.UsedCount >= d.MaxUses) return (null, 0, "ظرفیت استفاده از این کد تمام شده است.");
        if (subtotal < d.MinOrderAmount) return (null, 0, $"حداقل مبلغ سفارش برای این کد {d.MinOrderAmount / 10:N0} تومان است.");

        var amount = d.Type == DiscountType.Percent
            ? Math.Round(subtotal * d.Value / 100m, 0, MidpointRounding.AwayFromZero)
            : d.Value;
        amount = Math.Min(amount, subtotal);
        return (d, amount, null);
    }

    public async Task<QuoteResult> QuoteAsync(QuoteRequest req, CancellationToken ct)
    {
        var items = Normalize(req.Items);
        var ids = items.Select(i => i.ProductId).ToList();
        var products = await db.Products.AsNoTracking()
            .Where(p => ids.Contains(p.Id) && p.IsPublished)
            .Select(p => new
            {
                p.Id, p.Name, p.Slug, p.SalePrice, p.DiscountPercent, p.StockQuantity,
                ImageUrl = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()
            })
            .ToDictionaryAsync(p => p.Id, ct);

        var lines = new List<QuoteLine>();
        foreach (var it in items)
        {
            if (!products.TryGetValue(it.ProductId, out var p))
            {
                lines.Add(new QuoteLine(it.ProductId, "محصول نامعتبر", "", null, it.Quantity, 0, 0, 0, 0, false, "این محصول دیگر در دسترس نیست."));
                continue;
            }
            var unit = FinalUnitPrice(p.SalePrice, p.DiscountPercent);
            string? problem = p.StockQuantity <= 0 ? "ناموجود"
                : it.Quantity > p.StockQuantity ? $"موجودی کافی نیست (موجود: {p.StockQuantity})"
                : null;
            lines.Add(new QuoteLine(p.Id, p.Name, p.Slug, p.ImageUrl, it.Quantity, p.SalePrice, unit, unit * it.Quantity,
                p.StockQuantity, p.StockQuantity > 0, problem));
        }

        var subtotal = lines.Sum(l => l.LineTotal);

        decimal shipping = 0;
        if (req.ShippingMethodId is int sid)
        {
            var method = await db.ShippingMethods.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sid && s.IsActive, ct)
                ?? throw new DomainException("روش ارسال نامعتبر است.");
            shipping = method.Price;
        }

        var (disc, amount, error) = await EvaluateDiscountAsync(req.DiscountCode, subtotal, ct);
        var total = subtotal - amount + shipping;
        return new QuoteResult(lines, subtotal, amount, disc?.Code, error, shipping, total, lines.Count > 0 && lines.All(l => l.Problem is null));
    }

    // ---- سبد ذخیره‌شدهٔ کاربر واردشده ----

    public async Task<List<CartItemDto>> GetAsync(int userId, CancellationToken ct) =>
        await db.Carts.AsNoTracking().Where(c => c.UserId == userId)
            .SelectMany(c => c.Items).OrderBy(i => i.Id)
            .Select(i => new CartItemDto(i.ProductId, i.Quantity)).ToListAsync(ct);

    /// کل سبد را با لیست جدید جایگزین می‌کند (محصولات منتشرنشده/نامعتبر حذف می‌شوند).
    public async Task<List<CartItemDto>> ReplaceAsync(int userId, IEnumerable<CartItemDto>? input, CancellationToken ct)
    {
        var items = Normalize(input);
        var ids = items.Select(i => i.ProductId).ToList();
        var valid = await db.Products.AsNoTracking().Where(p => ids.Contains(p.Id) && p.IsPublished).Select(p => p.Id).ToListAsync(ct);
        items = items.Where(i => valid.Contains(i.ProductId)).ToList();

        var cart = await db.Carts.Include(c => c.Items).FirstOrDefaultAsync(c => c.UserId == userId, ct);
        if (cart is null)
        {
            cart = new Cart { UserId = userId };
            db.Carts.Add(cart);
        }

        var removed = cart.Items.Where(e => items.All(i => i.ProductId != e.ProductId)).ToList();
        db.CartItems.RemoveRange(removed);
        foreach (var i in items)
        {
            var existing = cart.Items.FirstOrDefault(e => e.ProductId == i.ProductId);
            if (existing is null) cart.Items.Add(new CartItem { ProductId = i.ProductId, Quantity = i.Quantity });
            else existing.Quantity = i.Quantity;
        }
        cart.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return items;
    }

    /// ادغام سبد مهمان (از مرورگر) با سبد حساب کاربری هنگام ورود.
    public async Task<List<CartItemDto>> MergeAsync(int userId, IEnumerable<CartItemDto>? guest, CancellationToken ct)
    {
        var current = await GetAsync(userId, ct);
        return await ReplaceAsync(userId, current.Concat(guest ?? []), ct);
    }
}
