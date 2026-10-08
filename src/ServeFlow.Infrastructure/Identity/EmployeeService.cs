using System.Data;
using Microsoft.Data.SqlClient;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServeFlow.Application.Accounts;
using ServeFlow.Application.Security;
using ServeFlow.Infrastructure.Persistence;

namespace ServeFlow.Infrastructure.Identity;

public sealed class EmployeeService(ServeFlowDbContext db, UserManager<ApplicationUser> users,
    IAccountActivity activity, IValidator<CreateEmployeeRequest> createValidator,
    IValidator<EditEmployeeRequest> editValidator, ILogger<EmployeeService> logger) : IEmployeeService
{
    public async Task<EmployeeDirectory> ListAsync(Guid actorId, CancellationToken cancellationToken)
    {
        var actor = await GetAdministratorAsync(actorId);
        if (actor is null) return new EmployeeDirectory(false, []);
        var accounts = await db.Users.AsNoTracking().Where(x => x.RestaurantId == actor.RestaurantId)
            .OrderBy(x => x.DisplayName).Take(500).ToListAsync(cancellationToken);
        var ids = accounts.Select(x => x.Id).ToArray();
        var roles = await (from link in db.UserRoles join role in db.Roles on link.RoleId equals role.Id
            where ids.Contains(link.UserId) select new { link.UserId, role.Name }).ToListAsync(cancellationToken);
        return new EmployeeDirectory(true, accounts.Select(x => new EmployeeSummary(x.Id, x.Email ?? "",
            x.DisplayName, roles.FirstOrDefault(r => r.UserId == x.Id && StaffRoles.EmployeeRoles.Contains(r.Name!))?.Name ?? "",
            x.IsArchived, x.ConcurrencyStamp ?? "")).ToArray());
    }

    public async Task<OperationResult> CreateAsync(Guid actorId, CreateEmployeeRequest request, CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return new OperationResult(false, validation.Errors.Select(x => x.ErrorMessage).ToArray());
        return await ExecuteAsync(async () =>
        {
            var actor = await GetAdministratorAsync(actorId);
            if (actor is null) return OperationResult.Denied();
            var user = new ApplicationUser { Id = Guid.NewGuid(), RestaurantId = actor.RestaurantId,
                Email = request.Email.Trim(), UserName = request.Email.Trim(), DisplayName = request.DisplayName.Trim() };
            var created = await users.CreateAsync(user, request.Password);
            if (!created.Succeeded) return Errors(created);
            var role = await users.AddToRoleAsync(user, request.Role);
            if (!role.Succeeded) return Errors(role);
            logger.LogInformation("Administrator {ActorId} created employee {EmployeeId}", actorId, user.Id);
            return OperationResult.Success();
        }, cancellationToken);
    }

    public async Task<OperationResult> UpdateAsync(Guid actorId, EditEmployeeRequest request, CancellationToken cancellationToken)
    {
        var validation = await editValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid) return new OperationResult(false, validation.Errors.Select(x => x.ErrorMessage).ToArray());
        return await ExecuteAsync(async () =>
        {
            var actor = await GetAdministratorAsync(actorId);
            if (actor is null) return OperationResult.Denied();
            var target = await db.Users.SingleOrDefaultAsync(x => x.Id == request.Id && x.RestaurantId == actor.RestaurantId, cancellationToken);
            if (target is null) return OperationResult.Denied();
            if (target.ConcurrencyStamp != request.ConcurrencyStamp) return OperationResult.Stale();
            if (target.Id == actor.Id && (request.IsArchived || request.Role != StaffRoles.Administrator))
                return OperationResult.Failure("You cannot archive your own account or remove your administrator role.");
            var oldRoles = await users.GetRolesAsync(target);
            if (!target.IsArchived && oldRoles.Contains(StaffRoles.Administrator) &&
                (request.IsArchived || request.Role != StaffRoles.Administrator))
            {
                var activeAdmins = await (from user in db.Users join link in db.UserRoles on user.Id equals link.UserId
                    join role in db.Roles on link.RoleId equals role.Id
                    where user.RestaurantId == actor.RestaurantId && !user.IsArchived && role.Name == StaffRoles.Administrator
                    select user.Id).CountAsync(cancellationToken);
                if (activeAdmins <= 1) return OperationResult.Failure("Keep at least one active administrator.");
            }
            target.DisplayName = request.DisplayName.Trim();
            target.IsArchived = request.IsArchived;
            var updated = await users.UpdateAsync(target);
            if (!updated.Succeeded) return Errors(updated);
            var removed = await users.RemoveFromRolesAsync(target, oldRoles);
            if (!removed.Succeeded) return Errors(removed);
            var added = await users.AddToRoleAsync(target, request.Role);
            if (!added.Succeeded) return Errors(added);
            var stamp = await users.UpdateSecurityStampAsync(target);
            if (!stamp.Succeeded) return Errors(stamp);
            logger.LogInformation("Administrator {ActorId} updated employee {EmployeeId}; role {Role}; archived {Archived}",
                actorId, target.Id, request.Role, request.IsArchived);
            return OperationResult.Success();
        }, cancellationToken);
    }

    private async Task<ApplicationUser?> GetAdministratorAsync(Guid actorId)
    {
        var actor = await users.FindByIdAsync(actorId.ToString());
        return actor is not null && await activity.IsActiveAsync(actor) && await users.IsInRoleAsync(actor, StaffRoles.Administrator)
            ? actor : null;
    }

    private async Task<OperationResult> ExecuteAsync(Func<Task<OperationResult>> action, CancellationToken cancellationToken)
    {
        try
        {
            return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
                var result = await action();
                if (result.Succeeded) await transaction.CommitAsync(cancellationToken);
                return result;
            });
        }
        catch (DbUpdateConcurrencyException) { return OperationResult.Stale(); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return OperationResult.Failure("An account with that email already exists.");
        }
    }

    private static OperationResult Errors(IdentityResult result) => result.Errors.Any(x => x.Code == "ConcurrencyFailure")
        ? OperationResult.Stale() : new OperationResult(false, result.Errors.Select(x => x.Description).ToArray());
}
