using Microsoft.EntityFrameworkCore;
using ServeFlow.Application.Accounts;
using ServeFlow.Application.Management;
using ServeFlow.Domain.Entities;

namespace ServeFlow.Infrastructure.Management;

public sealed partial class RestaurantManagementService
{
    public async Task<ManagementRead<ManagementPage<CategoryDetails>>> CategoriesAsync(Guid actorId, int page, string? search, bool includeArchived, CancellationToken token)
    {
        var restaurantId = await RestaurantAsync(actorId);
        if (restaurantId is null) return new(false, null);
        page = Page(page); var term = Search(search);
        var query = db.MenuCategories.AsNoTracking().Where(x => x.RestaurantId == restaurantId);
        if (!includeArchived) query = query.Where(x => !x.IsArchived);
        if (term.Length > 0) query = query.Where(x => x.Name.Contains(term));
        var count = await query.CountAsync(token);
        var rows = await query.OrderBy(x => x.SortOrder).ThenBy(x => x.Name).ThenBy(x => x.Id)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(token);
        return new(true, new(rows.Select(Category).ToArray(), page, count, PageSize, term, includeArchived, ""));
    }

    public async Task<ManagementRead<CategoryDetails>> GetCategoryAsync(Guid actorId, Guid id, CancellationToken token)
    {
        var restaurantId = await RestaurantAsync(actorId);
        if (restaurantId is null) return new(false, null);
        var entity = await db.MenuCategories.AsNoTracking().SingleOrDefaultAsync(x => x.RestaurantId == restaurantId && x.Id == id, token);
        return new(true, entity is null ? null : Category(entity));
    }

    public async Task<ManagementRead<IReadOnlyList<CategoryChoice>>> CategoryChoicesAsync(Guid actorId, CancellationToken token)
    {
        var restaurantId = await RestaurantAsync(actorId);
        if (restaurantId is null) return new(false, null);
        return new(true, await db.MenuCategories.AsNoTracking().Where(x => x.RestaurantId == restaurantId)
            .OrderBy(x => x.SortOrder).ThenBy(x => x.Name).Select(x => new CategoryChoice(x.Id, x.Name, x.IsArchived)).ToListAsync(token));
    }

    public async Task<OperationResult> SaveCategoryAsync(Guid actorId, CategoryInput input, CancellationToken token)
    {
        var validation = await categoryValidator.ValidateAsync(input, token);
        if (!validation.IsValid) return Validation<CategoryInput>(validation);
        return await WriteAsync(actorId, async restaurantId =>
        {
            var entity = input.Id is null ? new MenuCategory { RestaurantId = restaurantId }
                : await db.MenuCategories.SingleOrDefaultAsync(x => x.Id == input.Id && x.RestaurantId == restaurantId, token);
            if (entity is null) return OperationResult.Denied();
            if (input.Id is not null && !CheckVersion(db, entity, input.Version)) return OperationResult.Stale("This record changed. Reload the page and try again.");
            if (input.IsArchived && await db.MenuItems.AnyAsync(x => x.MenuCategoryId == entity.Id && !x.IsArchived, token))
                return OperationResult.Failure("Archive or move this category's active products first.");
            entity.Name = input.Name.Trim(); entity.SortOrder = input.SortOrder; entity.IsArchived = input.IsArchived;
            if (input.Id is null) db.MenuCategories.Add(entity);
            await db.SaveChangesAsync(token);
            return OperationResult.Success();
        }, token);
    }

    private static CategoryDetails Category(MenuCategory entity) => new(entity.Id, entity.Name, entity.SortOrder,
        entity.IsArchived, Convert.ToBase64String(entity.Version));
}
