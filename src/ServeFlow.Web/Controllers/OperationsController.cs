using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServeFlow.Application.Operations;
using ServeFlow.Application.Security;

namespace ServeFlow.Web.Controllers;

[Authorize(Policy = StaffPolicies.Staff)]
public sealed class OperationsController(IOperationsService operations) : Controller
{
    private Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet] public IActionResult Index() => View();
    [HttpGet("api/operations/queue"), EnableRateLimiting("operations-read")]
    public async Task<IActionResult> Queue(CancellationToken token)
    {
        var result = await operations.QueueAsync(Actor, token);
        return result.Allowed ? Ok(result.Value) : Forbid();
    }
    [HttpPost("api/operations/orders/{id:guid}/status"), RequestSizeLimit(4096), EnableRateLimiting("operations-write")]
    public async Task<IActionResult> Change([FromRoute] Guid id, [FromBody] StatusChange request, CancellationToken token)
    {
        if (!ModelState.IsValid || request is null) return BadRequest(new { error = "Check the status fields." });
        var result = await operations.ChangeAsync(Actor, id, request, token);
        if (result.Forbidden) return Forbid();
        return result.Succeeded ? Ok(new { saved = true }) : StatusCode(result.Conflict ? 409 : 400, new { error = string.Join(" ", result.Errors) });
    }
}
