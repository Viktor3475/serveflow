using System.Collections.Concurrent;

namespace ServeFlow.Web.Realtime;

public sealed record OrderSubscription(string ConnectionId, Guid? Actor, Guid? RestaurantId, string? Stamp,
    Guid? OrderId, string? ReceiptHash, DateTimeOffset ExpiresAt, Action Abort);
public sealed class OrderConnections
{
    private readonly ConcurrentDictionary<string, OrderSubscription> _subscriptions = new();
    public IReadOnlyList<OrderSubscription> All => _subscriptions.Values.ToArray();
    public void Set(OrderSubscription subscription) => _subscriptions[subscription.ConnectionId] = subscription;
    public void Remove(string id) => _subscriptions.TryRemove(id, out _);
    public bool Remove(OrderSubscription expected) => ((ICollection<KeyValuePair<string, OrderSubscription>>)_subscriptions).Remove(new(expected.ConnectionId, expected));
    public OrderSubscription? Get(string id) => _subscriptions.TryGetValue(id, out var subscription) ? subscription : null;
}
