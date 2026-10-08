using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServeFlow.Application.Analytics;
using ServeFlow.Application.Security;

namespace ServeFlow.Web.Controllers;

[Authorize(Policy = StaffPolicies.Administration), EnableRateLimiting("analytics-read")]
public sealed class DashboardController(IAnalyticsService analytics) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, CancellationToken token)
    {
        if (!ModelState.IsValid) return BadRequest("Use valid calendar dates in YYYY-MM-DD format.");
        var result = await analytics.ReadAsync(Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), new(from, to), token);
        if (!result.Allowed) return Forbid();
        if (result.Error is not null) { Response.StatusCode = 400; ViewData["AnalyticsError"] = result.Error; }
        return View(result.Data);
    }
}
