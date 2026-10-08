using Microsoft.AspNetCore.SignalR;

namespace ServeFlow.Web.Realtime;

public sealed class OrdersHubRateFilter : IHubFilter
{
    private sealed class Window { public DateTimeOffset Start = DateTimeOffset.UtcNow; public int Count; }
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocation, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        const string key = "serveflow:hub-invocations";
        if (!invocation.Context.Items.TryGetValue(key, out var value)) invocation.Context.Items[key] = value = new Window();
        var window = (Window)value!;
        lock (window)
        {
            if (DateTimeOffset.UtcNow - window.Start >= TimeSpan.FromMinutes(1)) { window.Start = DateTimeOffset.UtcNow; window.Count = 0; }
            if (++window.Count > 20) throw new HubException("Too many subscription requests. Try again shortly.");
        }
        return await next(invocation);
    }
}
