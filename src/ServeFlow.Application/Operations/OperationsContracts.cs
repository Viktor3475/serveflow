using ServeFlow.Application.Accounts;
using ServeFlow.Application.Management;
using ServeFlow.Application.Ordering;
using ServeFlow.Application.Security;
using ServeFlow.Domain.Enums;

namespace ServeFlow.Application.Operations;

public sealed record StaffAccess(Guid ActorId, Guid RestaurantId, string Stamp, IReadOnlyList<string> Roles);
public sealed record StatusChange(OrderStatus Status, string Version, string? Reason);
public sealed record QueueOrder(Guid Id, string Number, int TableNumber, OrderStatus Status, DateTimeOffset CreatedAt,
    string? Notes, string Version, IReadOnlyList<ReceiptLine> Lines, IReadOnlyList<OrderStatus> Actions);
public sealed record OrderQueue(IReadOnlyList<QueueOrder> Orders, int TotalCount);
public interface IOperationsService
{
    Task<StaffAccess?> AccessAsync(Guid actor, CancellationToken token);
    Task<ManagementRead<OrderQueue>> QueueAsync(Guid actor, CancellationToken token);
    Task<OperationResult> ChangeAsync(Guid actor, Guid orderId, StatusChange request, CancellationToken token);
    Task<bool> CanReadReceiptAsync(Guid orderId, string hash, CancellationToken token);
}
public interface IOrderChangeNotifier
{
    Task NotifyAsync(Guid orderId, Guid restaurantId, CancellationToken token);
}
public static class OrderTransitions
{
    public static IReadOnlyList<OrderStatus> Allowed(OrderStatus status, IReadOnlyList<string> roles)
    {
        var admin = roles.Contains(StaffRoles.Administrator);
        var kitchen = admin || roles.Contains(StaffRoles.KitchenStaff);
        var waiter = admin || roles.Contains(StaffRoles.Waiter);
        var next = new List<OrderStatus>();
        if (kitchen && status == OrderStatus.Pending) next.Add(OrderStatus.Accepted);
        if (kitchen && status == OrderStatus.Accepted) next.Add(OrderStatus.Preparing);
        if (kitchen && status == OrderStatus.Preparing) next.Add(OrderStatus.Ready);
        if (waiter && status == OrderStatus.Ready) next.Add(OrderStatus.Served);
        if (waiter && status == OrderStatus.Served) next.Add(OrderStatus.Completed);
        if ((kitchen && status is OrderStatus.Pending or OrderStatus.Accepted or OrderStatus.Preparing) ||
            (admin && status == OrderStatus.Ready) || (waiter && status == OrderStatus.Pending)) next.Add(OrderStatus.Cancelled);
        return next;
    }
    public static bool Terminal(OrderStatus status) => status is OrderStatus.Completed or OrderStatus.Cancelled;
}
