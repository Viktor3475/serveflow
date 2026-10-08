using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServeFlow.Application.Management;
using ServeFlow.Application.Security;
using ServeFlow.Web.Models;

namespace ServeFlow.Web.Controllers;

[Authorize(Policy = StaffPolicies.Administration)]
public sealed class CategoriesController(IRestaurantManagementService management) : ManagementController(management)
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, string? search = null, bool includeArchived = false, CancellationToken token = default)
    {
        var result = await Management.CategoriesAsync(ActorId, page, search, includeArchived, token);
        return result.Allowed ? View(result.Value) : Forbid();
    }
    [HttpGet] public IActionResult Create() => View("Form", new CategoryModel());
    [HttpGet]
    public async Task<IActionResult> Edit([FromRoute] Guid id, CancellationToken token)
    {
        var result = await Management.GetCategoryAsync(ActorId, id, token);
        if (!result.Allowed) return Forbid();
        var x = result.Value;
        return x is null ? NotFound() : View("Form", new CategoryModel { Id = x.Id, Name = x.Name, SortOrder = x.SortOrder, IsArchived = x.IsArchived, Version = x.Version });
    }
    [HttpPost] public Task<IActionResult> Create(CategoryModel model, CancellationToken token) => Save(null, model, token);
    [HttpPost]
    public Task<IActionResult> Edit([FromRoute] Guid id, CategoryModel model, CancellationToken token) => id != model.Id
        ? Task.FromResult<IActionResult>(BadRequest()) : Save(id, model, token);
    private async Task<IActionResult> Save(Guid? id, CategoryModel model, CancellationToken token)
    {
        if (ModelState.IsValid)
        {
            var result = await Management.SaveCategoryAsync(ActorId, new CategoryInput(id, model.Name, model.SortOrder, model.IsArchived, model.Version), token);
            if (result.Forbidden) return Forbid();
            if (result.Succeeded) return Saved("Category saved.");
            AddErrors(result);
        }
        if (id is null) model.Id = Guid.Empty;
        return View("Form", model);
    }
}
