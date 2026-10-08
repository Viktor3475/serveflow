using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ServeFlow.Application.Security;
using ServeFlow.Domain.Entities;
using ServeFlow.Infrastructure.Persistence;

namespace ServeFlow.Infrastructure.Identity;

public sealed class DevelopmentDemoProvisioner(ServeFlowDbContext db, UserManager<ApplicationUser> users,
    AdministratorProvisioner administrator, IConfiguration configuration, IHostEnvironment environment)
{
    public async Task ProvisionAsync(CancellationToken token)
    {
        var settings = DevelopmentDemoSettings.Read(configuration, environment.IsDevelopment());
        // Validate both employee passwords before the administrator provisioning step writes anything.
        foreach (var (email, password) in new[] { (settings.WaiterEmail, settings.WaiterPassword), (settings.KitchenEmail, settings.KitchenPassword) })
            foreach (var validator in users.PasswordValidators)
                Ensure(await validator.ValidateAsync(users, new ApplicationUser { Email = email, UserName = email }, password));
        await administrator.ProvisionAsync(token);
        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
            await Employee(settings.WaiterEmail, settings.WaiterPassword, "Demo waiter", StaffRoles.Waiter);
            await Employee(settings.KitchenEmail, settings.KitchenPassword, "Demo kitchen", StaffRoles.KitchenStaff);
            var categories = await db.MenuCategories.Where(x => x.RestaurantId == settings.RestaurantId).ToListAsync(token);
            if (categories.Count == 0)
            {
                var category = new MenuCategory { RestaurantId = settings.RestaurantId, Name = "Demo favorites" };
                db.Add(category);
                db.AddRange(new MenuItem { RestaurantId = settings.RestaurantId, MenuCategoryId = category.Id, Name = "Seasonal risotto", Description = "Roasted seasonal vegetables and aged cheese.", Price = 14.50m },
                    new MenuItem { RestaurantId = settings.RestaurantId, MenuCategoryId = category.Id, Name = "Burrata salad", Description = "Tomatoes, basil and extra virgin olive oil.", Price = 11m },
                    new MenuItem { RestaurantId = settings.RestaurantId, MenuCategoryId = category.Id, Name = "Lemon tart", Description = "A crisp pastry with fresh lemon curd.", Price = 6.50m });
            }
            if (!await db.RestaurantTables.AnyAsync(x => x.RestaurantId == settings.RestaurantId, token))
                for (var number = 1; number <= 6; number++) db.Add(new RestaurantTable { RestaurantId = settings.RestaurantId, Number = number, Capacity = number <= 4 ? 2 : 4 });
            await db.SaveChangesAsync(token);
            await transaction.CommitAsync(token);
        });

        async Task Employee(string email, string password, string name, string role)
        {
            var existing = await users.FindByEmailAsync(email);
            if (existing is not null)
            {
                if (existing.RestaurantId != settings.RestaurantId || existing.IsArchived ||
                    !(await users.GetRolesAsync(existing)).SequenceEqual([role]))
                    throw new InvalidOperationException("A demo account already exists with different ownership or access.");
                return; // Re-runs never reset passwords or roles.
            }
            var user = new ApplicationUser { Id = Guid.NewGuid(), RestaurantId = settings.RestaurantId, UserName = email, Email = email, DisplayName = name };
            Ensure(await users.CreateAsync(user, password));
            Ensure(await users.AddToRoleAsync(user, role));
        }
    }

    private static void Ensure(IdentityResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException("Demo provisioning failed: " + string.Join(", ", result.Errors.Select(x => x.Code)));
    }
}
