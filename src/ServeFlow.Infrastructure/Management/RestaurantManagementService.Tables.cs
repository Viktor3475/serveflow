using Microsoft.EntityFrameworkCore;
using ServeFlow.Application.Accounts;
using ServeFlow.Application.Management;
using ServeFlow.Domain.Entities;
using ServeFlow.Domain.Enums;

namespace ServeFlow.Infrastructure.Management;

public sealed partial class RestaurantManagementService
{
    public async Task<ManagementRead<ManagementPage<TableDetails>>> TablesAsync(Guid actorId, int page, bool includeArchived, CancellationToken token)
    {
        var restaurantId = await RestaurantAsync(actorId, diningRoom: true);
        if (restaurantId is null) return new(false, null);
        page = Page(page);
        var query = db.RestaurantTables.AsNoTracking().Where(x => x.RestaurantId == restaurantId);
        if (!includeArchived) query = query.Where(x => !x.IsArchived);
        var count = await query.CountAsync(token);
        var rows = await query.OrderBy(x => x.Number).Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(token);
        return new(true, new(rows.Select(Table).ToArray(), page, count, PageSize, "", includeArchived, ""));
    }

    public async Task<ManagementRead<TableDetails>> GetTableAsync(Guid actorId, Guid id, CancellationToken token)
    {
        var restaurantId = await RestaurantAsync(actorId, diningRoom: true);
        if (restaurantId is null) return new(false, null);
        var row = await db.RestaurantTables.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.RestaurantId == restaurantId, token);
        return new(true, row is null ? null : Table(row));
    }

    public async Task<OperationResult> SaveTableAsync(Guid actorId, TableInput input, CancellationToken token)
    {
        var validation = await tableValidator.ValidateAsync(input, token);
        if (!validation.IsValid) return Validation<TableInput>(validation);
        return await WriteAsync(actorId, async restaurantId =>
        {
            var entity = input.Id is null ? new RestaurantTable { RestaurantId = restaurantId }
                : await db.RestaurantTables.SingleOrDefaultAsync(x => x.Id == input.Id && x.RestaurantId == restaurantId, token);
            if (entity is null) return OperationResult.Denied();
            if (input.Id is not null && !CheckVersion(db, entity, input.Version)) return OperationResult.Stale("This record changed. Reload the page and try again.");
            var activeOrder = await HasActiveOrdersAsync(entity.Id, token);
            if ((activeOrder && input.Status != TableStatus.Occupied) || (input.IsArchived && (activeOrder || input.Status != TableStatus.Available)))
                return OperationResult.Failure("A table must be available with no active orders before it can be archived. Active orders require an occupied table.");
            entity.Number = input.Number; entity.Capacity = input.Capacity; entity.Status = input.Status; entity.IsArchived = input.IsArchived;
            if (input.Id is null) db.RestaurantTables.Add(entity);
            await db.SaveChangesAsync(token);
            return OperationResult.Success();
        }, token);
    }

    public Task<OperationResult> SetTableStatusAsync(Guid actorId, TableStatusInput input, CancellationToken token)
    {
        if (!Enum.IsDefined(input.Status) || ConcurrencyVersion.Decode(input.Version) is null)
            return Task.FromResult(OperationResult.Failure("Select a valid status and reload the table before saving."));
        return WriteAsync(actorId, async restaurantId =>
        {
            var entity = await db.RestaurantTables.SingleOrDefaultAsync(x => x.Id == input.Id && x.RestaurantId == restaurantId, token);
            if (entity is null || entity.IsArchived) return OperationResult.Denied();
            if (!CheckVersion(db, entity, input.Version)) return OperationResult.Stale("This record changed. Reload the page and try again.");
            if (input.Status != TableStatus.Occupied && await HasActiveOrdersAsync(entity.Id, token))
                return OperationResult.Failure("Complete or cancel this table's active orders before changing it from occupied.");
            entity.Status = input.Status;
            await db.SaveChangesAsync(token);
            return OperationResult.Success();
        }, token, diningRoom: true);
    }

    private Task<bool> HasActiveOrdersAsync(Guid tableId, CancellationToken token) => db.Orders.AnyAsync(
        x => x.RestaurantTableId == tableId && x.Status != OrderStatus.Completed && x.Status != OrderStatus.Cancelled, token);
    private static TableDetails Table(RestaurantTable table) => new(table.Id, table.Number, table.Capacity, table.Status, table.IsArchived, Convert.ToBase64String(table.Version));
}
