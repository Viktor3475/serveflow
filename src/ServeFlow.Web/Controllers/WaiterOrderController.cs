using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServeFlow.Application.Ordering;
using ServeFlow.Application.Security;

namespace ServeFlow.Web.Controllers;

[Authorize(Policy = StaffPolicies.DiningRoom)]
public sealed class WaiterOrderController(IOrderingService ordering) : Controller
{
    private Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public async Task<IActionResult> Index([FromRoute] Guid id, CancellationToken token)
    {
        if (await ordering.StaffMenuAsync(Actor, id, token) is null) return NotFound();
        return View("~/Views/Dining/Menu.cshtml", new DiningPage($"/api/staff/tables/{id}/menu", $"/api/staff/tables/{id}/orders", "/ProductImages/Show/"));
    }
    [HttpGet("api/staff/tables/{id:guid}/menu")]
    public async Task<IActionResult> Menu([FromRoute] Guid id, CancellationToken token)
    {
        var menu = await ordering.StaffMenuAsync(Actor, id, token);
        return menu is null ? NotFound() : Ok(menu);
    }
    [HttpPost("api/staff/tables/{id:guid}/orders"), RequestSizeLimit(32768), EnableRateLimiting("dining-order")]
    public async Task<IActionResult> Place([FromRoute] Guid id, [FromBody] PlaceOrderRequest request, CancellationToken token)
    {
        if (!ModelState.IsValid || request is null) return BadRequest(new { error = "Check your cart fields." });
        return DiningApiController.Result(await ordering.PlaceStaffAsync(Actor, id, request, token));
    }
}
