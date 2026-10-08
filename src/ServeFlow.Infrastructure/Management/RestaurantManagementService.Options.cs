using Microsoft.EntityFrameworkCore;
using FluentValidation;
using ServeFlow.Application.Accounts;
using ServeFlow.Application.Management;
using ServeFlow.Domain.Entities;

namespace ServeFlow.Infrastructure.Management;

public sealed partial class RestaurantManagementService : IMenuOptionService
{
    public async Task<ManagementRead<IReadOnlyList<OptionDetails>>> ListAsync(Guid actor, Guid item, CancellationToken token)
    {
        var restaurant = await RestaurantAsync(actor);
        if (restaurant is null || !await db.MenuItems.AnyAsync(x => x.Id == item && x.RestaurantId == restaurant, token)) return new(false, null);
        var rows = await db.MenuItemOptions.AsNoTracking().Where(x => x.MenuItemId == item && x.RestaurantId == restaurant).OrderBy(x => x.Name).ToListAsync(token);
        return new(true, rows.Select(x => new OptionDetails(x.Id, item, x.Name, x.AdditionalPrice, x.IsAvailable, x.IsArchived, Convert.ToBase64String(x.Version))).ToArray());
    }
    public async Task<OperationResult> SaveAsync(Guid actor, OptionInput input, CancellationToken token)
    {
        var validation = await new OptionInputValidator().ValidateAsync(input, token);
        if (!validation.IsValid) return Validation<OptionInput>(validation);
        return await WriteAsync(actor, async restaurant =>
        {
            if (!await db.MenuItems.AnyAsync(x => x.Id == input.ItemId && x.RestaurantId == restaurant, token)) return OperationResult.Denied();
            var option = input.Id is null ? new MenuItemOption { RestaurantId = restaurant, MenuItemId = input.ItemId }
                : await db.MenuItemOptions.SingleOrDefaultAsync(x => x.Id == input.Id && x.MenuItemId == input.ItemId && x.RestaurantId == restaurant, token);
            if (option is null) return OperationResult.Denied();
            if (input.Id is not null && !CheckVersion(db, option, input.Version)) return OperationResult.Stale("This option changed. Reload the page.");
            option.Name = input.Name.Trim(); option.AdditionalPrice = input.Price; option.IsArchived = input.Archived; option.IsAvailable = input.Available && !input.Archived;
            if (input.Id is null) db.MenuItemOptions.Add(option);
            await db.SaveChangesAsync(token);
            return OperationResult.Success();
        }, token);
    }
}
