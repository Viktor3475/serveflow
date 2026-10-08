using Microsoft.EntityFrameworkCore;
using ServeFlow.Application.Accounts;
using ServeFlow.Application.Management;

namespace ServeFlow.Infrastructure.Management;

public sealed partial class RestaurantManagementService
{
    public async Task<ManagementRead<RestaurantSettings>> GetSettingsAsync(Guid actorId, CancellationToken token)
    {
        var restaurantId = await RestaurantAsync(actorId);
        if (restaurantId is null) return new(false, null);
        var restaurant = await db.Restaurants.AsNoTracking().SingleAsync(x => x.Id == restaurantId, token);
        return new(true, new RestaurantSettings(restaurant.Name, restaurant.CurrencyCode, restaurant.TimeZoneId, Convert.ToBase64String(restaurant.Version)));
    }

    public async Task<OperationResult> SaveSettingsAsync(Guid actorId, SettingsInput input, CancellationToken token)
    {
        var validation = await settingsValidator.ValidateAsync(input, token);
        if (!validation.IsValid) return Validation<SettingsInput>(validation);
        return await WriteAsync(actorId, async restaurantId =>
        {
            var restaurant = await db.Restaurants.SingleAsync(x => x.Id == restaurantId, token);
            if (!CheckVersion(db, restaurant, input.Version)) return OperationResult.Stale("This record changed. Reload the page and try again.");
            // Currency remains unchanged: changing it would reinterpret existing menu prices.
            restaurant.Name = input.Name.Trim();
            restaurant.TimeZoneId = input.TimeZoneId;
            await db.SaveChangesAsync(token);
            return OperationResult.Success();
        }, token);
    }
}
