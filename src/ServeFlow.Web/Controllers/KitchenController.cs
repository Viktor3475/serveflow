using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServeFlow.Application.Security;

namespace ServeFlow.Web.Controllers;

[Authorize(Policy = StaffPolicies.Kitchen)]
public sealed class KitchenController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["KitchenMode"] = true;
        return View("~/Views/Operations/Index.cshtml");
    }
}
