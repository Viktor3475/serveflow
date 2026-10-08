using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ServeFlow.Application.Accounts;
using ServeFlow.Application.Management;

namespace ServeFlow.Web.Controllers;

public abstract class ManagementController(IRestaurantManagementService management) : Controller
{
    protected IRestaurantManagementService Management { get; } = management;
    protected Guid ActorId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;
    protected void AddErrors(OperationResult result)
    {
        if (result.Conflict) Response.StatusCode = StatusCodes.Status409Conflict;
        foreach (var error in result.Errors) ModelState.AddModelError("", error);
    }
    protected IActionResult Saved(string message)
    {
        TempData["Success"] = message;
        return RedirectToAction("Index");
    }
}
