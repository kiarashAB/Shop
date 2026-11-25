using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ReactApp1.Server.Domain;
using ReactApp1.Server.Infrastructure;
using ReactApp1.Server.Services;

namespace ReactApp1.Server.Controllers;

[ApiController, Route("api/cart")]
public class CartController(CartService cart) : ControllerBase
{
    /// قیمت‌گذاری سبد (مهمان یا واردشده): قیمت، موجودی، تخفیف، ارسال و جمع نهایی همیشه از سرور می‌آید.
    [HttpPost("quote"), AllowAnonymous, EnableRateLimiting("search")]
    public async Task<IActionResult> Quote(QuoteRequest req, CancellationToken ct) => Ok(await cart.QuoteAsync(req, ct));

    [HttpGet, Authorize(Roles = nameof(UserRole.Customer))]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await cart.GetAsync(User.GetUserId(), ct));

    [HttpPut, Authorize(Roles = nameof(UserRole.Customer))]
    public async Task<IActionResult> Replace(List<CartItemDto> items, CancellationToken ct) =>
        Ok(await cart.ReplaceAsync(User.GetUserId(), items, ct));

    /// ادغام سبد مهمان با سبد حساب کاربری هنگام ورود.
    [HttpPost("merge"), Authorize(Roles = nameof(UserRole.Customer))]
    public async Task<IActionResult> Merge(List<CartItemDto> guestItems, CancellationToken ct) =>
        Ok(await cart.MergeAsync(User.GetUserId(), guestItems, ct));
}
