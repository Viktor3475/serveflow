using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServeFlow.Application.Management;
using ServeFlow.Application.Ordering;
using ServeFlow.Web.Ordering;

namespace ServeFlow.Web.Controllers;

[AllowAnonymous]
public sealed class DiningController(IOrderingService ordering, TableSession session, IProductImageStore images) : Controller
{
    [HttpGet] public IActionResult Start() => View();
    [HttpGet] public IActionResult Index() => View("Menu", new DiningPage("/api/dining/menu", "/api/dining/orders", "/Dining/Image/"));
    [HttpGet] public IActionResult Receipt([FromRoute] Guid id) => View(id);
    [HttpGet, EnableRateLimiting("dining-read")]
    public async Task<IActionResult> Image([FromRoute] Guid id, CancellationToken token)
    {
        var access = session.Read(HttpContext);
        if (access is null) return NotFound();
        var key = await ordering.ImageAsync(access, id, token);
        var stream = key is null ? null : images.OpenRead(key);
        return stream is null ? NotFound() : File(stream, "image/png");
    }
}
public sealed record DiningPage(string MenuUrl, string OrderUrl, string ImageUrl);
