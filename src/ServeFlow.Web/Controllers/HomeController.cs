using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace ServeFlow.Web.Controllers;

[AllowAnonymous]
public sealed class HomeController : Controller
{
    [HttpGet]
    public IActionResult Index() => View();
}
