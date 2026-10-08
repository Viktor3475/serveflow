using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ServeFlow.Application.Security;
using ServeFlow.Domain.Entities;
using ServeFlow.Infrastructure.Persistence;

namespace ServeFlow.Infrastructure.Identity;

public sealed class AdministratorProvisioner(ServeFlowDbContext db, UserManager<ApplicationUser> users,
    RoleManager<IdentityRole<Guid>> roles, IConfiguration configuration)
{
    public async Task ProvisionAsync(CancellationToken cancellationToken)
    {
        var email = Required("Bootstrap:Email").Trim();
        var password = Required("Bootstrap:Password");
        var restaurantName = Required("Bootstrap:RestaurantName");
        var displayName = Required("Bootstrap:DisplayName");
        if (restaurantName.Length > 160 || displayName.Length > 160)
            throw new InvalidOperationException("Bootstrap names must be no longer than 160 characters.");
        if (!Guid.TryParse(Required("Bootstrap:RestaurantId"), out var restaurantId) || restaurantId == Guid.Empty)
            throw new InvalidOperationException("Bootstrap:RestaurantId must be a nonempty GUID.");
        if ((await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            throw new InvalidOperationException("Apply the database migrations before provisioning.");

        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
            foreach (var role in StaffRoles.All)
            {
                if (!await roles.RoleExistsAsync(role))
                    Ensure(await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.NewGuid() }));
            }
            var restaurant = await db.Restaurants.SingleOrDefaultAsync(x => x.Id == restaurantId, cancellationToken);
            if (restaurant is null)
            {
                db.Restaurants.Add(new Restaurant { Id = restaurantId, Name = restaurantName });
                await db.SaveChangesAsync(cancellationToken);
            }
            else if (restaurant.IsArchived) throw new InvalidOperationException("The configured restaurant is archived.");

            var existing = await users.FindByEmailAsync(email);
            if (existing is not null)
            {
                if (existing.RestaurantId != restaurantId || existing.IsArchived ||
                    !await users.IsInRoleAsync(existing, StaffRoles.Administrator))
                    throw new InvalidOperationException("The configured account already exists and is not this restaurant's active administrator.");
                // Idempotent re-run does not replace credentials or elevate an existing employee.
            }
            else
            {
                var user = new ApplicationUser { Id = Guid.NewGuid(), RestaurantId = restaurantId,
                    UserName = email, Email = email, DisplayName = displayName };
                Ensure(await users.CreateAsync(user, password));
                Ensure(await users.AddToRoleAsync(user, StaffRoles.Administrator));
            }
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private string Required(string key) => string.IsNullOrWhiteSpace(configuration[key])
        ? throw new InvalidOperationException($"Configure {key} externally before provisioning.") : configuration[key]!;

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded)
            // Identity descriptions can contain an email; output only stable error codes, never secrets.
            throw new InvalidOperationException("Provisioning failed: " + string.Join(", ", result.Errors.Select(x => x.Code)));
    }
}
