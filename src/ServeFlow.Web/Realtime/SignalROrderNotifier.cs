using Microsoft.AspNetCore.SignalR;
using ServeFlow.Application.Operations;

namespace ServeFlow.Web.Realtime;

public sealed class SignalROrderNotifier(IHubContext<OrdersHub> hub, OrderConnections connections, IServiceScopeFactory scopes,
    ILogger<SignalROrderNotifier> logger) : IOrderChangeNotifier
{
    public async Task NotifyAsync(Guid orderId, Guid restaurantId, CancellationToken token)
    {
        // A committed transaction must not become an HTTP failure when a transport is unavailable.
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            foreach (var subscription in connections.All.Where(x => x.RestaurantId == restaurantId || x.OrderId == orderId))
            {
                await using var scope = scopes.CreateAsyncScope();
                var operations = scope.ServiceProvider.GetRequiredService<IOperationsService>();
                if (!await OrderSubscriptionAccess.ValidAsync(subscription, operations, timeout.Token))
                { if (connections.Remove(subscription)) subscription.Abort(); continue; }
                if (connections.Get(subscription.ConnectionId) != subscription) continue;
                // No order IDs, totals, names or receipt capabilities are sent in refresh hints.
                await hub.Clients.Client(subscription.ConnectionId).SendAsync("OrdersChanged", timeout.Token);
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Live order refresh delivery failed; clients will resynchronize through HTTP");
        }
    }
}
