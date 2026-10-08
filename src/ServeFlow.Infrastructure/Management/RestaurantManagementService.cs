using System.Data;
using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ServeFlow.Application.Accounts;
using ServeFlow.Application.Management;
using ServeFlow.Application.Security;
using ServeFlow.Infrastructure.Identity;
using ServeFlow.Infrastructure.Persistence;

namespace ServeFlow.Infrastructure.Management;

public sealed partial class RestaurantManagementService(
    ServeFlowDbContext db, UserManager<ApplicationUser> users, IAccountActivity activity,
    IValidator<SettingsInput> settingsValidator, IValidator<CategoryInput> categoryValidator,
    IValidator<MenuInput> menuValidator, IValidator<TableInput> tableValidator, IProductImageStore images,
    ILogger<RestaurantManagementService> logger) : IRestaurantManagementService
{
    private const int PageSize = 20;
    private async Task<Guid?> RestaurantAsync(Guid actorId, bool diningRoom = false, bool anyStaff = false)
    {
        var actor = await users.FindByIdAsync(actorId.ToString());
        if (actor is null || !await activity.IsActiveAsync(actor)) return null;
        var roles = await users.GetRolesAsync(actor);
        var allowed = roles.Contains(StaffRoles.Administrator)
            || (diningRoom && roles.Contains(StaffRoles.Waiter))
            || (anyStaff && roles.Any(StaffRoles.EmployeeRoles.Contains));
        return allowed ? actor.RestaurantId : null;
    }

    private async Task<OperationResult> WriteAsync(Guid actorId, Func<Guid, Task<OperationResult>> action,
        CancellationToken token, bool diningRoom = false)
    {
        try
        {
            var result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
                var restaurantId = await RestaurantAsync(actorId, diningRoom);
                if (restaurantId is null) return OperationResult.Denied();
                var result = await action(restaurantId.Value);
                if (result.Succeeded) await transaction.CommitAsync(token);
                return result;
            });
            if (result.Succeeded) logger.LogInformation("Staff {ActorId} committed a restaurant management change", actorId);
            return result;
        }
        catch (DbUpdateConcurrencyException) { return OperationResult.Stale("This record changed. Reload the page and try again."); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        { return OperationResult.Failure("That table number already exists in this restaurant."); }
    }

    private static bool CheckVersion<T>(ServeFlowDbContext context, T entity, string? version) where T : class
    {
        var bytes = ConcurrencyVersion.Decode(version);
        if (bytes is null) return false;
        var property = context.Entry(entity).Property<byte[]>("Version");
        if (!property.CurrentValue!.SequenceEqual(bytes)) return false;
        property.OriginalValue = bytes;
        return true;
    }

    private static string Search(string? search) => (search ?? "").Trim()[..Math.Min((search ?? "").Trim().Length, 100)];
    private static int Page(int page) => Math.Clamp(page, 1, 10000);
    private static OperationResult Validation<T>(FluentValidation.Results.ValidationResult result) =>
        new(false, result.Errors.Select(x => x.ErrorMessage).ToArray());
}
