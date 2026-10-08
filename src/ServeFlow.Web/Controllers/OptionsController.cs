using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServeFlow.Application.Management;
using ServeFlow.Application.Security;

namespace ServeFlow.Web.Controllers;

[Authorize(Policy = StaffPolicies.Administration)]
public sealed class OptionsController(IMenuOptionService options) : Controller
{
    private Guid Actor => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    [HttpGet]
    public async Task<IActionResult> Index([FromRoute] Guid id, CancellationToken token)
    {
        var result = await options.ListAsync(Actor, id, token);
        return result.Allowed ? View(new OptionPage(id, result.Value!)) : NotFound();
    }
    [HttpGet]
    public async Task<IActionResult> Edit([FromRoute] Guid id, Guid? optionId, CancellationToken token)
    {
        var result = await options.ListAsync(Actor, id, token);
        if (!result.Allowed) return NotFound();
        var option = result.Value!.SingleOrDefault(x => x.Id == optionId);
        if (optionId is not null && option is null) return NotFound();
        return View(option is null ? new OptionInput(null, id, "", 0, true, false, null) : new OptionInput(option.Id, id, option.Name, option.Price, option.Available, option.Archived, option.Version));
    }
    [HttpPost]
    public async Task<IActionResult> Edit([FromRoute] Guid id, OptionInput model, CancellationToken token)
    {
        if (id != model.ItemId) return BadRequest();
        if (ModelState.IsValid)
        {
            var result = await options.SaveAsync(Actor, model, token);
            if (result.Forbidden) return Forbid();
            if (result.Succeeded) return RedirectToAction("Index", new { id });
            if (result.Conflict) Response.StatusCode = 409;
            foreach (var error in result.Errors) ModelState.AddModelError("", error);
        }
        return View(model);
    }
}
public sealed record OptionPage(Guid ItemId, IReadOnlyList<OptionDetails> Options);
