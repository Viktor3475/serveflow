using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRCoder;
using ServeFlow.Application.Management;
using ServeFlow.Application.Ordering;
using ServeFlow.Application.Security;

namespace ServeFlow.Web.Controllers;

[Authorize(Policy = StaffPolicies.Administration)]
public sealed class TableQrController(IRestaurantManagementService management, IOrderingService ordering, IConfiguration configuration, IHostEnvironment environment) : Controller
{
    private Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public async Task<IActionResult> Index([FromRoute] Guid id, CancellationToken token)
    {
        var result = await management.GetTableAsync(Actor, id, token);
        return !result.Allowed ? Forbid() : result.Value is null || result.Value.IsArchived ? NotFound() : View(result.Value);
    }
    [HttpPost]
    public async Task<IActionResult> Generate([FromRoute] Guid id, string version, CancellationToken token)
    {
        // Generate links from a configured origin, never an untrusted request Host header.
        var origin = configuration["Ordering:PublicOrigin"] ?? (environment.IsDevelopment() ? "http://localhost:5080" : "");
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) || (uri.Scheme != "https" && !(environment.IsDevelopment() && uri.Scheme == "http"))
            || uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0)
            throw new InvalidOperationException("Configure Ordering:PublicOrigin as the external HTTPS origin.");
        var issued = await ordering.RotateQrAsync(Actor, id, version, token);
        if (issued is null) return Conflict("The table changed or is unavailable. Reload before generating a QR code.");
        var link = uri.GetLeftPart(UriPartial.Authority) + "/Dining/Start#" + issued.Token;
        using var data = QRCodeGenerator.GenerateQrCode(link, QRCodeGenerator.ECCLevel.Q);
        using var qr = new SvgQRCode(data);
        return File(Encoding.UTF8.GetBytes(qr.GetGraphic(8)), "image/svg+xml", $"serveflow-table-{issued.TableNumber}.svg");
    }
}
