using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;

namespace ReactApp1.Server.Controllers;

/// APIهای عمومی فروشگاه (بدون احراز هویت). قیمت خرید/بهای تمام‌شده هرگز در پاسخ نیست.
[ApiController, Route("api")]
public class CatalogController(AppDbContext db) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<IActionResult> Categories(CancellationToken ct) =>
        Ok(await db.Categories.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.SortOrder)
            .Select(c => new { c.Id, c.Name, c.Slug, c.ImageUrl, c.ParentId, ProductCount = c.Products.Count(p => p.IsPublished) })
            .ToListAsync(ct));

    [HttpGet("banners")]
    public async Task<IActionResult> Banners([FromQuery] BannerPlacement placement, CancellationToken ct) =>
        Ok(await db.Banners.AsNoTracking().Where(b => b.IsActive && b.Placement == placement).OrderBy(b => b.SortOrder)
            .Select(b => new { b.Id, b.Title, b.Subtitle, b.DiscountLabel, b.ButtonText, b.ImageUrl, b.LinkUrl }).ToListAsync(ct));

    [HttpGet("products")]
    public async Task<IActionResult> Products([FromQuery] string? categorySlug, [FromQuery] string? search, [FromQuery] bool discounted,
        [FromQuery] string sort = "new", [FromQuery] int page = 1, [FromQuery] int pageSize = 12, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 48);
        page = Math.Max(page, 1);
        var q = db.Products.AsNoTracking().Where(p => p.IsPublished);
        if (!string.IsNullOrWhiteSpace(categorySlug)) q = q.Where(p => p.Category!.Slug == categorySlug);
        if (!string.IsNullOrWhiteSpace(search)) q = q.Where(p => p.Name.Contains(search) || p.Sku == search || p.PartNumber == search);
        if (discounted) q = q.Where(p => p.DiscountPercent > 0);
        q = sort switch
        {
            "best" => q.OrderByDescending(p => p.SoldCount).ThenByDescending(p => p.Id),
            "price" => q.OrderBy(p => p.SalePrice),
            _ => q.OrderByDescending(p => p.CreatedAt)
        };
        var total = await q.CountAsync(ct);
        var rows = await q.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(p => new
            {
                p.Id, p.Name, p.Slug, p.SalePrice, p.DiscountPercent, InStock = p.StockQuantity > 0,
                CategoryName = p.Category!.Name, BrandName = p.Brand != null ? p.Brand.Name : null,
                ImageUrl = p.Images.OrderBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault()
            }).ToListAsync(ct);
        var items = rows.Select(p => new
        {
            p.Id, p.Name, p.Slug, p.ImageUrl, p.InStock, p.CategoryName, p.BrandName, p.DiscountPercent,
            Price = p.SalePrice,
            FinalPrice = Math.Round(p.SalePrice * (1 - p.DiscountPercent / 100m), 0, MidpointRounding.AwayFromZero)
        });
        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("products/{slug}")]
    public async Task<IActionResult> ProductBySlug(string slug, CancellationToken ct)
    {
        var p = await db.Products.AsNoTracking().Where(x => x.Slug == slug && x.IsPublished)
            .Select(x => new
            {
                x.Id, x.Name, x.Slug, x.Sku, x.PartNumber, x.Description, x.SalePrice, x.DiscountPercent,
                InStock = x.StockQuantity > 0, x.Warranty, Condition = x.Condition.ToString(),
                x.SeoTitle, x.SeoDescription, x.SeoKeywords,
                CategoryName = x.Category!.Name, CategorySlug = x.Category!.Slug,
                BrandName = x.Brand != null ? x.Brand.Name : null,
                Images = x.Images.OrderBy(i => i.SortOrder).Select(i => new { i.Url, i.Alt }),
                Specifications = x.Specifications.Select(s => new { s.Name, s.Value })
            }).FirstOrDefaultAsync(ct);
        if (p is null) return NotFound();
        return Ok(new
        {
            p.Id, p.Name, p.Slug, p.Sku, p.PartNumber, p.Description, p.InStock, p.Warranty, p.Condition,
            p.SeoTitle, p.SeoDescription, p.SeoKeywords, p.CategoryName, p.CategorySlug, p.BrandName,
            p.Images, p.Specifications, p.DiscountPercent,
            Price = p.SalePrice,
            FinalPrice = Math.Round(p.SalePrice * (1 - p.DiscountPercent / 100m), 0, MidpointRounding.AwayFromZero)
        });
    }
}
