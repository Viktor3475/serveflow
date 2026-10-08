using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServeFlow.Web.Configuration;

namespace ServeFlow.IntegrationTests;

public sealed class TrustedProxyTests
{
    [Fact]
    public void EmptyConfigurationDoesNotEnableProxyTrust() => Assert.Empty(TrustedProxyConfiguration.Read(new ConfigurationBuilder().Build()));
    [Theory]
    [InlineData("*")] [InlineData("proxy.example.com")]
    public void NamesAndWildcardsAreRejected(string value)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["ReverseProxy:KnownProxies:0"] = value }).Build();
        Assert.Throws<InvalidOperationException>(() => TrustedProxyConfiguration.Read(configuration));
    }
    [Theory]
    [InlineData("10.89.0.2", "https|203.0.113.17")]
    [InlineData("10.89.0.3", "http|10.89.0.3")]
    public async Task ActualMiddlewareOnlyAcceptsHeadersFromConfiguredPeer(string peer, string expected)
    {
        using var host = await new HostBuilder().ConfigureWebHost(web => web.UseTestServer().ConfigureServices(services =>
        {
            services.Configure<ForwardedHeadersOptions>(options => TrustedProxyConfiguration.Apply(options, [IPAddress.Parse("10.89.0.2")]));
        }).Configure(app =>
        {
            app.Use((context, next) => { context.Connection.RemoteIpAddress = IPAddress.Parse(peer); return next(context); });
            app.UseForwardedHeaders();
            app.Run(context => context.Response.WriteAsync(context.Request.Scheme + "|" + context.Connection.RemoteIpAddress));
        })).StartAsync(TestContext.Current.CancellationToken);
        using var client = host.GetTestClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("X-Forwarded-For", "203.0.113.17"); request.Headers.Add("X-Forwarded-Proto", "https");
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expected, await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
}
