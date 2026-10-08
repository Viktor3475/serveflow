using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServeFlow.Application.Management;
using ServeFlow.Application.Security;
using ServeFlow.Web.Models;

namespace ServeFlow.Web.Controllers;

[Authorize(Policy = StaffPolicies.DiningRoom)]
public sealed class TablesController(IRestaurantManagementService management) : ManagementController(management)
{
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, bool includeArchived = false, CancellationToken token = default)
    {
        var result = await Management.TablesAsync(ActorId, page, includeArchived, token);
        return result.Allowed ? View(result.Value) : Forbid();
    }
    [HttpGet, Authorize(Policy = StaffPolicies.Administration)] public IActionResult Create() => View("Form", new TableModel());
    [HttpGet, Authorize(Policy = StaffPolicies.Administration)]
    public async Task<IActionResult> Edit([FromRoute] Guid id, CancellationToken token)
    {
        var result = await Management.GetTableAsync(ActorId, id, token);
        if (!result.Allowed) return Forbid();
        var x = result.Value;
        return x is null ? NotFound() : View("Form", new TableModel { Id = x.Id, Number = x.Number, Capacity = x.Capacity, Status = x.Status, IsArchived = x.IsArchived, Version = x.Version });
    }
    [HttpPost, Authorize(Policy = StaffPolicies.Administration)] public Task<IActionResult> Create(TableModel model, CancellationToken token) => Save(null, model, token);
    [HttpPost, Authorize(Policy = StaffPolicies.Administration)] public Task<IActionResult> Edit([FromRoute] Guid id, TableModel model, CancellationToken token) => id != model.Id
        ? Task.FromResult<IActionResult>(BadRequest()) : Save(id, model, token);
    private async Task<IActionResult> Save(Guid? id, TableModel model, CancellationToken token)
    {
        if (ModelState.IsValid)
        {
            var result = await Management.SaveTableAsync(ActorId, new TableInput(id, model.Number, model.Capacity, model.Status, model.IsArchived, model.Version), token);
            if (result.Forbidden) return Forbid();
            if (result.Succeeded) return Saved("Table saved.");
            AddErrors(result);
        }
        if (id is null) model.Id = Guid.Empty;
        return View("Form", model);
    }
    [HttpGet]
    public async Task<IActionResult> Status([FromRoute] Guid id, CancellationToken token)
    {
        var result = await Management.GetTableAsync(ActorId, id, token);
        if (!result.Allowed) return Forbid();
        var x = result.Value;
        return x is null || x.IsArchived ? NotFound() : View(new TableStatusModel { Id = x.Id, Number = x.Number, Status = x.Status, Version = x.Version });
    }
    [HttpPost]
    public async Task<IActionResult> Status([FromRoute] Guid id, TableStatusModel model, CancellationToken token)
    {
        if (id != model.Id) return BadRequest();
        if (ModelState.IsValid)
        {
            var result = await Management.SetTableStatusAsync(ActorId, new TableStatusInput(id, model.Status, model.Version), token);
            if (result.Forbidden) return Forbid();
            if (result.Succeeded) return Saved("Table status updated.");
            AddErrors(result);
        }
        var current = await Management.GetTableAsync(ActorId, id, token);
        if (!current.Allowed) return Forbid();
        if (current.Value is null) return NotFound();
        model.Number = current.Value.Number;
        return View(model);
    }
}
