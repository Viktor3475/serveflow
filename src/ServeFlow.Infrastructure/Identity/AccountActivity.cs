using Microsoft.EntityFrameworkCore;
using ServeFlow.Infrastructure.Persistence;

namespace ServeFlow.Infrastructure.Identity;

public interface IAccountActivity
{
    Task<bool> IsActiveAsync(ApplicationUser user);
}

public sealed class AccountActivity(ServeFlowDbContext db) : IAccountActivity
{
    public Task<bool> IsActiveAsync(ApplicationUser user) => user.IsArchived
        ? Task.FromResult(false)
        : db.Restaurants.AnyAsync(x => x.Id == user.RestaurantId && !x.IsArchived);
}
