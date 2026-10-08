using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using ServeFlow.Application.Operations;
using ServeFlow.Application.Ordering;
using ServeFlow.Application.Security;
using ServeFlow.Infrastructure.Ordering;

namespace ServeFlow.Web.Realtime;

[AllowAnonymous]
public sealed class OrdersHub(IOperationsService operations, OrderConnections connections, IOptions<IdentityOptions> identityOptions) : Hub
{
    [Authorize(Policy = StaffPolicies.Staff)]
    public async Task SubscribeStaff()
    {
        if (!Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier), out var actor)) throw new HubException("Staff access is unavailable.");
        var access = await operations.AccessAsync(actor, Context.ConnectionAborted);
        var stamp = Context.User?.FindFirstValue(identityOptions.Value.ClaimsIdentity.SecurityStampClaimType);
        if (access is null || access.Stamp != stamp) { Context.Abort(); throw new HubException("Sign in again to refresh staff access."); }
        connections.Set(new(Context.ConnectionId, actor, access.RestaurantId, stamp, null, null, DateTimeOffset.UtcNow.AddMinutes(2), Context.Abort));
    }
    public async Task SubscribeReceipt(Guid orderId, string receiptToken)
    {
        if (!CapabilityTokens.Valid(receiptToken)) throw new HubException("Receipt access is unavailable.");
        var hash = OrderingService.Hash(receiptToken);
        if (!await operations.CanReadReceiptAsync(orderId, hash, Context.ConnectionAborted)) throw new HubException("Receipt access is unavailable.");
        connections.Set(new(Context.ConnectionId, null, null, null, orderId, hash, DateTimeOffset.UtcNow.AddMinutes(2), Context.Abort));
    }
    public async Task Renew()
    {
        var subscription = connections.Get(Context.ConnectionId);
        if (subscription is null || !await OrderSubscriptionAccess.ValidAsync(subscription, operations, Context.ConnectionAborted))
        { connections.Remove(Context.ConnectionId); Context.Abort(); throw new HubException("Refresh access before reconnecting."); }
        connections.Set(subscription with { ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(2) });
    }
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
public static class OrderSubscriptionAccess
{
    public static async Task<bool> ValidAsync(OrderSubscription subscription, IOperationsService operations, CancellationToken token)
    {
        if (subscription.ExpiresAt <= DateTimeOffset.UtcNow) return false;
        if (subscription.Actor is { } actor)
        {
            var access = await operations.AccessAsync(actor, token);
            return access is not null && access.RestaurantId == subscription.RestaurantId && access.Stamp == subscription.Stamp;
        }
        return subscription.OrderId is { } id && subscription.ReceiptHash is { } hash && await operations.CanReadReceiptAsync(id, hash, token);
    }
}
