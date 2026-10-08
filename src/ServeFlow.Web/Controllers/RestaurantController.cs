using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServeFlow.Application.Management;
using ServeFlow.Application.Security;
using ServeFlow.Web.Models;

namespace ServeFlow.Web.Controllers;

[Authorize(Policy = StaffPolicies.Administration)]
public sealed class RestaurantController(IRestaurantManagementService management) : ManagementController(management)
{
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken token)
    {
        var result = await Management.GetSettingsAsync(ActorId, token);
        if (!result.Allowed) return Forbid();
        var x = result.Value!;
        return View(new RestaurantSettingsModel { Name = x.Name, TimeZoneId = x.TimeZoneId, CurrencyCode = x.CurrencyCode, Version = x.Version });
    }
    [HttpPost]
    public async Task<IActionResult> Index(RestaurantSettingsModel model, CancellationToken token)
    {
        if (ModelState.IsValid)
        {
            var result = await Management.SaveSettingsAsync(ActorId, new SettingsInput(model.Name, model.TimeZoneId, model.Version), token);
            if (result.Forbidden) return Forbid();
            if (result.Succeeded) return Saved("Restaurant settings updated.");
            AddErrors(result);
        }
        var settings = await Management.GetSettingsAsync(ActorId, token);
        if (!settings.Allowed) return Forbid();
        model.CurrencyCode = settings.Value!.CurrencyCode;
        return View(model);
    }
}
