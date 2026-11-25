using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReactApp1.Server.Data;
using ReactApp1.Server.Domain;
using ReactApp1.Server.Infrastructure;
using ReactApp1.Server.Services;

namespace ReactApp1.Server.Controllers;

static class OrderViews
{
    public static object Detail(Order o) => new
    {
        o.Id, o.Number, Status = o.Status.ToString(), o.CreatedAt,
        o.RecipientName, o.Phone, o.Province, o.City, o.PostalCode, o.FullAddress, o.ShippingMethodName,
        o.Subtotal, o.DiscountAmount, o.DiscountCode, o.ShippingCost, o.Total, o.PaidAmount,
        PaymentState = o.PaidAmount >= o.Total ? "Paid" : o.PaidAmount > 0 ? "Partial" : "Unpaid",
        o.Note, o.CancelReason, o.CancelledAt,
        Items = o.Items.Select(i => new { i.ProductId, i.ProductName, i.Sku, i.Slug, i.ImageUrl, i.Quantity, i.ListPrice, i.UnitPrice, i.LineTotal }),
        History = o.History.OrderBy(h => h.At).ThenBy(h => h.Id).Select(h => new { Status = h.ToStatus.ToString(), h.At, h.Note }),
        Shipment = o.Shipment is null ? null : new { o.Shipment.Carrier, o.Shipment.TrackingCode, o.Shipment.ShippedAt, o.Shipment.Note }
    };

    public static IQueryable<Order> Full(AppDbContext db) =>
        db.Orders.AsNoTracking().Include(o => o.Items).Include(o => o.History).Include(o => o.Shipment).AsSplitQuery();
}

/// سفارش‌های مشتری واردشده. شناسهٔ کاربر همیشه از کوکی نشست می‌آید؛ مشتری هرگز سفارش دیگران را نمی‌بیند.
[ApiController, Route("api/my/orders"), Authorize(Roles = nameof(UserRole.Customer))]
public class MyOrdersController(AppDbContext db, OrderService orders) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 10, CancellationToken ct = default)
    {
        var uid = User.GetUserId();
        pageSize = Math.Clamp(pageSize, 1, 50);
        page = Math.Max(page, 1);
        var q = db.Orders.AsNoTracking().Where(o => o.UserId == uid);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(o => o.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new
            {
                o.Id, o.Number, o.CreatedAt, o.Total, o.PaidAmount, o.Status,
                ItemCount = o.Items.Sum(i => i.Quantity)
            }).ToListAsync(ct);
        var items = rows.Select(o => new
        {
            o.Id, o.Number, o.CreatedAt, o.Status, o.Total, o.PaidAmount, o.ItemCount,
            PaymentState = o.PaidAmount >= o.Total ? "Paid" : o.PaidAmount > 0 ? "Partial" : "Unpaid"
        });
        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var uid = User.GetUserId();
        var o = await OrderViews.Full(db).FirstOrDefaultAsync(x => x.Id == id && x.UserId == uid, ct);
        return o is null ? NotFound() : Ok(OrderViews.Detail(o));
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateOrderDto dto, CancellationToken ct)
    {
        var o = await orders.CreateAsync(User.GetUserId(), dto, ct);
        return Ok(new { o.Id, o.Number, o.Total });
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancelOrderDto dto, CancellationToken ct)
    {
        var uid = User.GetUserId();
        if (!await db.Orders.AnyAsync(x => x.Id == id && x.UserId == uid, ct)) return NotFound();
        var o = await orders.CancelAsync(id, dto.Reason, uid, byCustomer: true, ct);
        return Ok(new { o.Id, o.Number, Status = o.Status.ToString() });
    }
}

/// مدیریت سفارش‌ها در پنل مدیریت.
[ApiController, Route("api/admin/orders"), Authorize(Roles = AppRoles.OrderView)]
public class AdminOrdersController(AppDbContext db, OrderService orders) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] OrderStatus? status, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);
        var q = db.Orders.AsNoTracking().AsQueryable();
        if (status is not null) q = q.Where(o => o.Status == status);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = Validation.NormalizeDigits(search.Trim());
            q = q.Where(o => o.Number.Contains(s) || o.RecipientName.Contains(s) || o.Phone.Contains(s));
        }
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(o => o.Id).Skip((page - 1) * pageSize).Take(pageSize)
            .Select(o => new
            {
                o.Id, o.Number, o.CreatedAt, o.RecipientName, o.Phone, o.Total, o.PaidAmount, o.Status,
                ItemCount = o.Items.Sum(i => i.Quantity)
            }).ToListAsync(ct);
        var items = rows.Select(o => new
        {
            o.Id, o.Number, o.CreatedAt, o.RecipientName, o.Phone, o.Status, o.Total, o.PaidAmount, o.ItemCount,
            PaymentState = o.PaidAmount >= o.Total ? "Paid" : o.PaidAmount > 0 ? "Partial" : "Unpaid"
        });
        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var o = await OrderViews.Full(db).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (o is null) return NotFound();
        var payments = await db.Payments.AsNoTracking().Where(p => p.OrderId == id).OrderBy(p => p.Id)
            .Select(p => new { p.Id, p.Number, p.Amount, p.Method, p.Date, p.ReferenceNumber, p.Note }).ToListAsync(ct);
        var customer = await db.Parties.AsNoTracking().Where(p => p.Id == o.PartyId)
            .Select(p => new { p.Id, p.Name, p.Phone, p.Email }).FirstOrDefaultAsync(ct);
        return Ok(new { order = OrderViews.Detail(o), payments, customer });
    }

    [HttpPost("{id:int}/status"), Authorize(Roles = AppRoles.Orders)]
    public async Task<IActionResult> ChangeStatus(int id, OrderStatusDto dto, CancellationToken ct)
    {
        var o = await orders.ChangeStatusAsync(id, dto.Status, dto.Note, User.GetUserId(), ct);
        return Ok(new { o.Id, Status = o.Status.ToString() });
    }

    [HttpPost("{id:int}/shipment"), Authorize(Roles = AppRoles.Orders)]
    public async Task<IActionResult> SetShipment(int id, ShipmentDto dto, CancellationToken ct)
    {
        var o = await orders.SetShipmentAsync(id, dto, User.GetUserId(), ct);
        return Ok(new { o.Id, Status = o.Status.ToString() });
    }

    [HttpPost("{id:int}/cancel"), Authorize(Roles = AppRoles.Orders)]
    public async Task<IActionResult> Cancel(int id, CancelOrderDto dto, CancellationToken ct)
    {
        var o = await orders.CancelAsync(id, dto.Reason, User.GetUserId(), byCustomer: false, ct);
        return Ok(new { o.Id, Status = o.Status.ToString() });
    }

    [HttpPost("{id:int}/payments"), Authorize(Roles = AppRoles.OrdersAndFinance)]
    public async Task<IActionResult> RegisterPayment(int id, OrderPaymentDto dto, CancellationToken ct)
    {
        var o = await orders.RegisterPaymentAsync(id, dto, User.GetUserId(), ct);
        return Ok(new { o.Id, Status = o.Status.ToString(), o.PaidAmount, o.Total });
    }
}
