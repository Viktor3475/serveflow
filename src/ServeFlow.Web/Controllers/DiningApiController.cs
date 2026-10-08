using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServeFlow.Application.Ordering;
using ServeFlow.Web.Ordering;

namespace ServeFlow.Web.Controllers;

[AllowAnonymous, Route("api/dining"), RequestSizeLimit(32768)]
public sealed class DiningApiController(IOrderingService ordering, TableSession session) : ControllerBase
{
    [HttpPost("session"), EnableRateLimiting("dining-exchange")]
    public async Task<IActionResult> Exchange([FromBody] ExchangeRequest request, CancellationToken token)
    {
        if (!ModelState.IsValid || request is null || !CapabilityTokens.Valid(request.Token)) return BadRequest(new { error = "Scan a valid table QR code." });
        var access = await ordering.ExchangeAsync(request.Token, token);
        if (access is null) return NotFound(new { error = "This QR code is unavailable. Ask a waiter for the current code." });
        session.Issue(HttpContext, access);
        return Ok(new { tableId = access.TableId });
    }
    [HttpGet("menu"), EnableRateLimiting("dining-read")]
    public async Task<IActionResult> Menu(CancellationToken token)
    {
        var access = session.Read(HttpContext);
        var menu = access is null ? null : await ordering.MenuAsync(access, token);
        return menu is null ? StatusCode(403, new { error = "Table access expired. Scan the current QR code." }) : Ok(menu);
    }
    [HttpPost("orders"), EnableRateLimiting("dining-order")]
    public async Task<IActionResult> Place([FromBody] PlaceOrderRequest request, CancellationToken token)
    {
        if (!ModelState.IsValid || request is null) return BadRequest(new { error = "Check your cart fields." });
        var access = session.Read(HttpContext);
        if (access is null) return StatusCode(403, new { error = "Scan a table QR code before ordering." });
        return Result(await ordering.PlaceAsync(access, request, token));
    }
    [HttpGet("receipts/{id:guid}"), EnableRateLimiting("dining-read")]
    public async Task<IActionResult> Receipt([FromRoute] Guid id, CancellationToken token)
    {
        var secret = Request.Headers["X-Receipt-Token"].ToString();
        var receipt = await ordering.ReceiptAsync(id, secret, token);
        return receipt is null ? NotFound(new { error = "Receipt access is unavailable. Open the original receipt link." }) : Ok(receipt);
    }
    [HttpGet("receipts/recover"), EnableRateLimiting("dining-read")]
    public Task<IActionResult> Recover(CancellationToken token) => Receipt(Guid.Empty, token);
    internal static ObjectResult Result(OrderingResult result) => result.Order is not null ? new ObjectResult(result.Order) { StatusCode = result.Order.Replayed ? 200 : 201 }
        : new ObjectResult(new { error = result.Error }) { StatusCode = result.Forbidden ? 403 : result.Throttled ? 429 : result.Conflict ? 409 : 400 };
}
public sealed record ExchangeRequest(string Token);
