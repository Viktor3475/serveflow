using Microsoft.EntityFrameworkCore;
using ServeFlow.Application.Accounts;
using ServeFlow.Application.Management;
using ServeFlow.Domain.Entities;

namespace ServeFlow.Infrastructure.Management;

public sealed partial class RestaurantManagementService
{
    public async Task<ManagementRead<ManagementPage<MenuDetails>>> MenuAsync(Guid actorId, int page, string? search, bool includeArchived, CancellationToken token)
    {
        var restaurantId = await RestaurantAsync(actorId);
        if (restaurantId is null) return new(false, null);
        page = Page(page); var term = Search(search);
        var query = db.MenuItems.AsNoTracking().Where(x => x.RestaurantId == restaurantId);
        if (!includeArchived) query = query.Where(x => !x.IsArchived);
        if (term.Length > 0) query = query.Where(x => x.Name.Contains(term) || x.Description.Contains(term));
        var count = await query.CountAsync(token);
        var rows = await (from item in query join category in db.MenuCategories on item.MenuCategoryId equals category.Id
            orderby category.SortOrder, item.Name, item.Id select new { Item = item, CategoryName = category.Name })
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(token);
        var currency = await db.Restaurants.Where(x => x.Id == restaurantId).Select(x => x.CurrencyCode).SingleAsync(token);
        return new(true, new(rows.Select(x => Menu(x.Item, x.CategoryName)).ToArray(), page, count, PageSize, term, includeArchived, currency));
    }

    public async Task<ManagementRead<MenuDetails>> GetMenuAsync(Guid actorId, Guid id, CancellationToken token)
    {
        var restaurantId = await RestaurantAsync(actorId);
        if (restaurantId is null) return new(false, null);
        var row = await (from item in db.MenuItems.AsNoTracking() join category in db.MenuCategories on item.MenuCategoryId equals category.Id
            where item.Id == id && item.RestaurantId == restaurantId select new { Item = item, CategoryName = category.Name }).SingleOrDefaultAsync(token);
        return new(true, row is null ? null : Menu(row.Item, row.CategoryName));
    }

    public async Task<OperationResult> SaveMenuAsync(Guid actorId, MenuInput input, CancellationToken token)
    {
        var validation = await menuValidator.ValidateAsync(input, token);
        if (!validation.IsValid) return Validation<MenuInput>(validation);
        return await WriteAsync(actorId, async restaurantId =>
        {
            var category = await db.MenuCategories.SingleOrDefaultAsync(x => x.Id == input.CategoryId && x.RestaurantId == restaurantId, token);
            if (category is null || (!input.IsArchived && category.IsArchived))
                return OperationResult.Failure("Select an active category from this restaurant.");
            var entity = input.Id is null ? new MenuItem { RestaurantId = restaurantId }
                : await db.MenuItems.SingleOrDefaultAsync(x => x.Id == input.Id && x.RestaurantId == restaurantId, token);
            if (entity is null) return OperationResult.Denied();
            if (input.Id is not null && !CheckVersion(db, entity, input.Version)) return OperationResult.Stale("This record changed. Reload the page and try again.");
            entity.MenuCategoryId = input.CategoryId; entity.Name = input.Name.Trim(); entity.Description = input.Description.Trim();
            entity.Price = input.Price; entity.IsArchived = input.IsArchived; entity.IsAvailable = !input.IsArchived && input.IsAvailable;
            if (input.Id is null) db.MenuItems.Add(entity);
            await db.SaveChangesAsync(token);
            return OperationResult.Success();
        }, token);
    }

    private static MenuDetails Menu(MenuItem entity, string categoryName) => new(entity.Id, entity.MenuCategoryId, categoryName,
        entity.Name, entity.Description, entity.Price, entity.IsAvailable, entity.IsArchived, entity.ImageStorageKey is not null,
        Convert.ToBase64String(entity.Version));
}
