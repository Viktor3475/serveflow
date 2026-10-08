using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServeFlow.Application.Accounts;
using ServeFlow.Application.Security;
using ServeFlow.Web.Models;

namespace ServeFlow.Web.Controllers;

[Authorize(Policy = StaffPolicies.Administration)]
public sealed class EmployeesController(IEmployeeService employees) : Controller
{
    private Guid ActorId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var result = await employees.ListAsync(ActorId, cancellationToken);
        return result.Allowed ? View(result.Employees) : Forbid();
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateEmployeeModel());

    [HttpPost]
    public async Task<IActionResult> Create(CreateEmployeeModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View(model);
        var result = await employees.CreateAsync(ActorId,
            new CreateEmployeeRequest(model.Email, model.DisplayName, model.Password, model.Role), cancellationToken);
        if (result.Forbidden) return Forbid();
        if (!result.Succeeded) { AddErrors(result); return View(model); }
        TempData["Success"] = "Employee account created.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var result = await employees.ListAsync(ActorId, cancellationToken);
        if (!result.Allowed) return Forbid();
        var employee = result.Employees.SingleOrDefault(x => x.Id == id);
        return employee is null ? NotFound() : View(new EditEmployeeModel
        {
            Id = employee.Id, DisplayName = employee.DisplayName, Role = employee.Role,
            IsArchived = employee.IsArchived, ConcurrencyStamp = employee.ConcurrencyStamp
        });
    }

    [HttpPost]
    public async Task<IActionResult> Edit(Guid id, EditEmployeeModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View(model);
        var result = await employees.UpdateAsync(ActorId,
            new EditEmployeeRequest(model.Id, model.DisplayName, model.Role, model.IsArchived, model.ConcurrencyStamp), cancellationToken);
        if (result.Forbidden) return Forbid();
        if (!result.Succeeded) { AddErrors(result); return View(model); }
        TempData["Success"] = "Employee account updated.";
        return RedirectToAction("Index");
    }

    private void AddErrors(OperationResult result)
    {
        if (result.Conflict) Response.StatusCode = StatusCodes.Status409Conflict;
        foreach (var error in result.Errors) ModelState.AddModelError(string.Empty, error);
    }
}
