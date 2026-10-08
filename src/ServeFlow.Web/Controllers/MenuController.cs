using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServeFlow.Application.Management;
using ServeFlow.Application.Security;
using ServeFlow.Web.Models;

namespace ServeFlow.Web.Controllers;

[Authorize(Policy = StaffPolicies.Administration)]
public sealed class MenuController(IRestaurantManagementService management) : ManagementController(management)
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, string? search = null, bool includeArchived = false, CancellationToken token = default)
    {
        var result = await Management.MenuAsync(ActorId, page, search, includeArchived, token);
        return result.Allowed ? View(result.Value) : Forbid();
    }
    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken token) => await Form(new MenuModel(), token);
    [HttpGet]
    public async Task<IActionResult> Edit([FromRoute] Guid id, CancellationToken token)
    {
        var result = await Management.GetMenuAsync(ActorId, id, token);
        if (!result.Allowed) return Forbid();
        var x = result.Value;
        return x is null ? NotFound() : await Form(new MenuModel { Id = x.Id, CategoryId = x.CategoryId, Name = x.Name,
            Description = x.Description, Price = x.Price, IsAvailable = x.IsAvailable, IsArchived = x.IsArchived, Version = x.Version, HasImage = x.HasImage }, token);
    }
    [HttpPost] public Task<IActionResult> Create(MenuModel model, CancellationToken token) => Save(null, model, token);
    [HttpPost] public Task<IActionResult> Edit([FromRoute] Guid id, MenuModel model, CancellationToken token) => id != model.Id
        ? Task.FromResult<IActionResult>(BadRequest()) : Save(id, model, token);
    private async Task<IActionResult> Save(Guid? id, MenuModel model, CancellationToken token)
    {
        if (ModelState.IsValid)
        {
            var result = await Management.SaveMenuAsync(ActorId, new MenuInput(id, model.CategoryId, model.Name, model.Description ?? "",
                model.Price, model.IsAvailable, model.IsArchived, model.Version), token);
            if (result.Forbidden) return Forbid();
            if (result.Succeeded) return Saved("Product saved.");
            AddErrors(result);
        }
        if (id is null) model.Id = Guid.Empty;
        else
        {
            var current = await Management.GetMenuAsync(ActorId, id.Value, token);
            if (!current.Allowed) return Forbid();
            model.HasImage = current.Value?.HasImage ?? false;
        }
        return await Form(model, token);
    }
    private async Task<IActionResult> Form(MenuModel model, CancellationToken token)
    {
        var categories = await Management.CategoryChoicesAsync(ActorId, token);
        if (!categories.Allowed) return Forbid();
        model.CategoryChoices = categories.Value!;
        return View("Form", model);
    }

    [HttpPost, RequestSizeLimit(6 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 5 * 1024 * 1024), EnableRateLimiting("image-upload")]
    public async Task<IActionResult> UploadImage([FromRoute] Guid id, ImageModel model, CancellationToken token)
    {
        if (!ModelState.IsValid || model.Image is null || model.Image.Length == 0 || model.Image.Length > 5 * 1024 * 1024)
            return BadRequest("Choose an image no larger than 5 MB and reload the product before saving.");
        await using var stream = model.Image.OpenReadStream();
        var result = await Management.UploadImageAsync(ActorId, id, model.Version, stream, model.Image.FileName, model.Image.ContentType, token);
        if (result.Forbidden) return Forbid();
        if (result.Succeeded) { TempData["Success"] = "Product image updated."; return RedirectToAction("Edit", new { id }); }
        AddErrors(result);
        return await Edit(id, token);
    }

    [HttpPost]
    public async Task<IActionResult> RemoveImage([FromRoute] Guid id, ImageModel model, CancellationToken token)
    {
        if (!ModelState.IsValid) return BadRequest();
        var result = await Management.RemoveImageAsync(ActorId, id, model.Version, token);
        if (result.Forbidden) return Forbid();
        if (result.Succeeded) { TempData["Success"] = "Product image removed."; return RedirectToAction("Edit", new { id }); }
        AddErrors(result);
        return await Edit(id, token);
    }
}
