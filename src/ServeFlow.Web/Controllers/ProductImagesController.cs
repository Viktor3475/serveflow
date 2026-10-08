using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServeFlow.Application.Management;
using ServeFlow.Application.Security;

namespace ServeFlow.Web.Controllers;

[Authorize(Policy = StaffPolicies.Staff)]
public sealed class ProductImagesController(IRestaurantManagementService management, IProductImageStore images) : ManagementController(management)
{
    [HttpGet]
    public async Task<IActionResult> Show([FromRoute] Guid id, CancellationToken token)
    {
        var result = await Management.ImageKeyAsync(ActorId, id, token);
        if (!result.Allowed) return Forbid();
        if (result.Value is null) return NotFound();
        var stream = images.OpenRead(result.Value);
        if (stream is null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        return File(stream, "image/png");
    }
}
