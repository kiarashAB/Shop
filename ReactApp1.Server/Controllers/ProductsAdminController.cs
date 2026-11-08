using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;
using ReactApp1.Server.Infrastructure;
using ReactApp1.Server.Services;

namespace ReactApp1.Server.Controllers;

/// موجودی از اینجا قابل ویرایش نیست؛ فقط از طریق اسناد انبار (رسید خرید، خروج، ...).
[ApiController, Route("api/admin/products"), Authorize(Roles = AppRoles.Catalog + ",InventoryManager,Accountant")]
public class ProductsAdminController(AppDbContext db, LedgerService ledger) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? search, CancellationToken ct)
    {
        var q = db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(p => p.Name.Contains(search) || p.Sku.Contains(search) || (p.PartNumber != null && p.PartNumber.Contains(search)));
        return Ok(await q.OrderByDescending(p => p.Id).Take(200)
            .Select(p => new { p.Id, p.Name, p.Sku, p.PartNumber, p.SalePrice, p.AverageCost, p.StockQuantity, p.IsPublished, Category = p.Category!.Name }).ToListAsync(ct));
    }

    [HttpPost, Authorize(Roles = AppRoles.Catalog)]
    public async Task<IActionResult> Create(ProductDto d, CancellationToken ct)
    {
        Validate(d);
        if (await db.Products.AnyAsync(p => p.Sku == d.Sku || p.Slug == d.Slug, ct)) throw new DomainException("SKU یا اسلاگ تکراری است.", 409);
        var p = new Product();
        Apply(p, d);
        db.Products.Add(p);
        ledger.Audit(User.GetUserId(), "ProductCreate", "Product", null, d.Sku);
        await db.SaveChangesAsync(ct);
        return Ok(new { p.Id });
    }

    [HttpPut("{id:int}"), Authorize(Roles = AppRoles.Catalog)]
    public async Task<IActionResult> Update(int id, ProductDto d, CancellationToken ct)
    {
        Validate(d);
        var p = await db.Products.Include(x => x.Images).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (p is null) return NotFound();
        if (await db.Products.AnyAsync(x => x.Id != id && (x.Sku == d.Sku || x.Slug == d.Slug), ct)) throw new DomainException("SKU یا اسلاگ تکراری است.", 409);
        Apply(p, d);
        p.ConcurrencyStamp = Guid.NewGuid();
        ledger.Audit(User.GetUserId(), "ProductUpdate", "Product", p.Id, d.Sku);
        await db.SaveChangesAsync(ct);
        return Ok(new { p.Id });
    }

    static void Validate(ProductDto d)
    {
        if (string.IsNullOrWhiteSpace(d.Name) || string.IsNullOrWhiteSpace(d.Sku) || string.IsNullOrWhiteSpace(d.Slug)) throw new DomainException("نام، SKU و اسلاگ الزامی است.");
        if (d.SalePrice < 0) throw new DomainException("قیمت فروش نامعتبر است.");
        if (d.DiscountPercent is < 0 or > 100) throw new DomainException("درصد تخفیف باید بین ۰ تا ۱۰۰ باشد.");
    }

    static void Apply(Product p, ProductDto d)
    {
        p.Name = d.Name.Trim(); p.Slug = d.Slug.Trim(); p.Sku = d.Sku.Trim(); p.PartNumber = d.PartNumber; p.CategoryId = d.CategoryId; p.BrandId = d.BrandId;
        p.Description = d.Description; p.SalePrice = d.SalePrice; p.DiscountPercent = d.DiscountPercent; p.Warranty = d.Warranty; p.Condition = d.Condition;
        p.IsPublished = d.IsPublished; p.SeoTitle = d.SeoTitle; p.SeoDescription = d.SeoDescription; p.SeoKeywords = d.SeoKeywords;
        if (d.ImageUrls is not null)
        {
            p.Images.Clear();
            for (var i = 0; i < d.ImageUrls.Count; i++) p.Images.Add(new ProductImage { Url = d.ImageUrls[i], Alt = d.Name, SortOrder = i });
        }
    }
}
