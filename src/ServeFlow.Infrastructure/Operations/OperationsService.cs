using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServeFlow.Application.Accounts;
using ServeFlow.Application.Management;
using ServeFlow.Application.Operations;
using ServeFlow.Application.Ordering;
using ServeFlow.Application.Security;
using ServeFlow.Domain.Entities;
using ServeFlow.Domain.Enums;
using ServeFlow.Infrastructure.Identity;
using ServeFlow.Infrastructure.Persistence;

namespace ServeFlow.Infrastructure.Operations;

public sealed class OperationsService(ServeFlowDbContext db, UserManager<ApplicationUser> users, IAccountActivity activity,
    IOrderChangeNotifier notifier) : IOperationsService
{
    public async Task<StaffAccess?> AccessAsync(Guid actor, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actor, token);
        if (user is null || !await activity.IsActiveAsync(user)) return null;
        var roles = await users.GetRolesAsync(user);
        return roles.Any(StaffRoles.EmployeeRoles.Contains) ? new(actor, user.RestaurantId, user.SecurityStamp!, roles.ToArray()) : null;
    }
    public async Task<ManagementRead<OrderQueue>> QueueAsync(Guid actor, CancellationToken token)
    {
        var access = await AccessAsync(actor, token);
        if (access is null) return new(false, null);
        var query = db.Orders.AsNoTracking().Where(x => x.RestaurantId == access.RestaurantId && x.Status != OrderStatus.Completed && x.Status != OrderStatus.Cancelled);
        if (access.Roles.Contains(StaffRoles.KitchenStaff) && !access.Roles.Contains(StaffRoles.Administrator) && !access.Roles.Contains(StaffRoles.Waiter))
            query = query.Where(x => x.Status == OrderStatus.Pending || x.Status == OrderStatus.Accepted || x.Status == OrderStatus.Preparing || x.Status == OrderStatus.Ready);
        var count = await query.CountAsync(token);
        var orders = await query.Include(x => x.Items).ThenInclude(x => x.Options).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).Take(100).ToListAsync(token);
        var tableIds = orders.Select(x => x.RestaurantTableId).Distinct().ToArray();
        var tables = await db.RestaurantTables.AsNoTracking().Where(x => x.RestaurantId == access.RestaurantId && tableIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Number, token);
        return new(true, new(orders.Select(x => new QueueOrder(x.Id, x.OrderNumber, tables[x.RestaurantTableId], x.Status, x.CreatedAt, x.Notes,
            Convert.ToBase64String(x.Version), x.Items.OrderBy(i => i.Id).Select(i => new ReceiptLine(i.NameSnapshot, i.UnitPriceSnapshot, i.Quantity, i.Instructions,
                i.Options.Select(o => new ReceiptOption(o.NameSnapshot, o.AdditionalPriceSnapshot)).ToArray())).ToArray(), OrderTransitions.Allowed(x.Status, access.Roles))).ToArray(), count));
    }
    public async Task<OperationResult> ChangeAsync(Guid actor, Guid orderId, StatusChange request, CancellationToken token)
    {
        var version = ConcurrencyVersion.Decode(request.Version);
        if (version is null || !Enum.IsDefined(request.Status) || request.Reason?.Length > 500 ||
            (request.Status == OrderStatus.Cancelled && string.IsNullOrWhiteSpace(request.Reason)))
            return OperationResult.Failure("Choose a valid status, reload the order, and provide a cancellation reason (up to 500 characters).");
        var target = await db.Orders.AsNoTracking().Where(x => x.Id == orderId).Select(x => new { x.RestaurantId, x.RestaurantTableId }).SingleOrDefaultAsync(token);
        if (target is null) return OperationResult.Denied();
        Guid changedRestaurant = default;
        OperationResult result;
        try
        {
            result = await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                db.ChangeTracker.Clear();
                await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, token);
                var access = await AccessAsync(actor, token);
                if (access is null || access.RestaurantId != target.RestaurantId) return OperationResult.Denied();
                var tableId = target.RestaurantTableId;
                // Match creation's table-first lock order; one transaction cannot release another active order's table.
                var table = await db.RestaurantTables.FromSqlInterpolated($"SELECT * FROM [RestaurantTables] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = {tableId}").SingleAsync(token);
                var order = await db.Orders.SingleAsync(x => x.Id == orderId && x.RestaurantId == access.RestaurantId, token);
                if (!order.Version.SequenceEqual(version)) return OperationResult.Stale("This order changed. Refresh before trying again.");
                if (!OrderTransitions.Allowed(order.Status, access.Roles).Contains(request.Status)) return OperationResult.Failure("Your role cannot make that status transition.");
                var previous = order.Status;
                order.Status = request.Status;
                db.OrderStatusHistory.Add(new OrderStatusHistory { RestaurantId = order.RestaurantId, OrderId = order.Id, ChangedByUserId = actor,
                    PreviousStatus = previous, NewStatus = request.Status, Reason = request.Reason?.Trim() });
                if (OrderTransitions.Terminal(request.Status) && !await db.Orders.AnyAsync(x => x.RestaurantTableId == tableId && x.Id != order.Id && x.Status != OrderStatus.Completed && x.Status != OrderStatus.Cancelled, token))
                    table.Status = TableStatus.Available;
                else table.Status = TableStatus.Occupied;
                await db.SaveChangesAsync(token);
                await transaction.CommitAsync(token);
                changedRestaurant = order.RestaurantId;
                return OperationResult.Success();
            });
        }
        catch (DbUpdateConcurrencyException) { return OperationResult.Stale("This order changed. Refresh before trying again."); }
        if (result.Succeeded) await notifier.NotifyAsync(orderId, changedRestaurant, CancellationToken.None);
        return result;
    }
    public Task<bool> CanReadReceiptAsync(Guid orderId, string hash, CancellationToken token) => db.Orders.AsNoTracking().AnyAsync(x => x.Id == orderId && x.ReceiptTokenHash == hash, token);
}
public sealed class NoOpOrderNotifier : IOrderChangeNotifier
{
    public Task NotifyAsync(Guid orderId, Guid restaurantId, CancellationToken token) => Task.CompletedTask;
}
