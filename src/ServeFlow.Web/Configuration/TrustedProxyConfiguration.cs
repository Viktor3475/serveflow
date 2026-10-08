using System.Net;
using Microsoft.AspNetCore.HttpOverrides;

namespace ServeFlow.Web.Configuration;

public static class TrustedProxyConfiguration
{
    public static IReadOnlyList<IPAddress> Read(IConfiguration configuration)
    {
        var values = configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>() ?? [];
        return values.Select(value => IPAddress.TryParse(value, out var address) ? address
            : throw new InvalidOperationException("ReverseProxy:KnownProxies must contain IP addresses, not names or wildcards.")).ToArray();
    }

    public static void Apply(ForwardedHeadersOptions options, IReadOnlyList<IPAddress> proxies)
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
        foreach (var proxy in proxies) options.KnownProxies.Add(proxy);
    }
}
