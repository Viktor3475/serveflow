using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using ServeFlow.Application.Accounts;
using ServeFlow.Application.Management;
using ServeFlow.Application.Operations;
using ServeFlow.Application.Security;
using ServeFlow.Domain.Enums;
using ServeFlow.Web.Realtime;

namespace ServeFlow.IntegrationTests;

public sealed class OperationsHttpTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Fact]
    public async Task AnonymousQueueRequiresStaffAuthentication()
    {
        await using var factory = new AuthenticationTests.AuthFactory { Operations = new Backend() }; using var client = AuthenticationTests.Client(factory);
        using var response = await client.GetAsync("/Operations", Token); Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }
    [Theory]
    [InlineData(StaffRoles.Administrator)] [InlineData(StaffRoles.KitchenStaff)] [InlineData(StaffRoles.Waiter)]
    public async Task StaffQueueRendersAndUsesServerActor(string role)
    {
        var backend = new Backend(); await using var factory = new AuthenticationTests.AuthFactory { Operations = backend }; using var client = AuthenticationTests.Client(factory);
        var actor = await AuthenticationTests.SeedAsync(factory, role); using var login = await AuthenticationTests.LoginAsync(client);
        using var page = await client.GetAsync("/Operations", Token); Assert.Equal(HttpStatusCode.OK, page.StatusCode); Assert.True(page.Headers.CacheControl!.NoStore);
        using var response = await client.GetAsync("/api/operations/queue", Token); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(actor.Id, backend.Actor);
    }
    [Fact]
    public async Task StatusWritesRequireCsrfAndStaleWritesReturnConflict()
    {
        var backend = new Backend { ChangeResult = OperationResult.Stale() }; await using var factory = new AuthenticationTests.AuthFactory { Operations = backend }; using var client = AuthenticationTests.Client(factory);
        var actor = await AuthenticationTests.SeedAsync(factory, StaffRoles.Administrator); using var login = await AuthenticationTests.LoginAsync(client);
        var request = new StatusChange(OrderStatus.Accepted, Convert.ToBase64String(new byte[8]), null);
        using var rejected = await client.PostAsJsonAsync($"/api/operations/orders/{Guid.NewGuid()}/status", request, Token); Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", await AuthenticationTests.GetTokenAsync(client, "/Operations"));
        using var stale = await client.PostAsJsonAsync($"/api/operations/orders/{Guid.NewGuid()}/status", request, Token); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode); Assert.Equal(actor.Id, backend.Actor);
    }
    [Fact]
    public async Task CrossOriginHubNegotiationIsRejected()
    {
        await using var factory = new AuthenticationTests.AuthFactory(); using var client = AuthenticationTests.Client(factory); client.DefaultRequestHeaders.Add("Origin", "https://outside.example");
        using var response = await client.PostAsync("/hubs/orders/negotiate?negotiateVersion=1", null, Token); Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    [Fact]
    public async Task AnonymousHubCannotSubscribeToStaffAndWrongReceiptIsRejected()
    {
        await using var factory = new AuthenticationTests.AuthFactory { Operations = new Backend() }; await using var hub = Hub(factory);
        await hub.StartAsync(Token);
        await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync("SubscribeStaff", Token));
        await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync("SubscribeReceipt", Guid.NewGuid(), new string('f', 64), Token));
        Assert.Empty(factory.Services.GetRequiredService<OrderConnections>().All);
    }
    [Fact]
    public async Task ReceiptHubReceivesOnlyItsOrderAndCanResubscribeAfterReconnect()
    {
        var backend = new Backend(); await using var factory = new AuthenticationTests.AuthFactory { Operations = backend }; await using var hub = Hub(factory);
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var count = 0;
        using var handler = hub.On("OrdersChanged", () => { Interlocked.Increment(ref count); received.TrySetResult(); });
        await hub.StartAsync(Token); await hub.InvokeAsync("SubscribeReceipt", backend.Order, Backend.Secret, Token);
        await using var scope = factory.Services.CreateAsyncScope(); var notifier = scope.ServiceProvider.GetRequiredService<IOrderChangeNotifier>();
        await notifier.NotifyAsync(Guid.NewGuid(), backend.Restaurant, Token); Assert.Equal(0, Volatile.Read(ref count));
        await notifier.NotifyAsync(backend.Order, backend.Restaurant, Token); await received.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.Equal(1, Volatile.Read(ref count));
        await hub.StopAsync(Token); await hub.StartAsync(Token); await hub.InvokeAsync("SubscribeReceipt", backend.Order, Backend.Secret, Token);
        Assert.Single(factory.Services.GetRequiredService<OrderConnections>().All);
        await hub.InvokeAsync("Renew", Token);
    }
    [Fact]
    public async Task StaffDeliveryRechecksRestaurantAndRevokedStamp()
    {
        var backend = new Backend(); await using var factory = new AuthenticationTests.AuthFactory { Operations = backend }; using var client = AuthenticationTests.Client(factory);
        var user = await AuthenticationTests.SeedAsync(factory, StaffRoles.KitchenStaff); backend.Access = new(user.Id, backend.Restaurant, user.SecurityStamp!, [StaffRoles.KitchenStaff]);
        using var login = await AuthenticationTests.LoginAsync(client); var cookie = string.Join("; ", login.Headers.GetValues("Set-Cookie").Where(x => x.StartsWith("ServeFlow.Auth=")).Select(x => x.Split(';')[0]));
        await using var hub = Hub(factory, cookie); var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var count = 0;
        using var handler = hub.On("OrdersChanged", () => { Interlocked.Increment(ref count); received.TrySetResult(); });
        await hub.StartAsync(Token); await hub.InvokeAsync("SubscribeStaff", Token);
        await using var scope = factory.Services.CreateAsyncScope(); var notifier = scope.ServiceProvider.GetRequiredService<IOrderChangeNotifier>();
        await notifier.NotifyAsync(Guid.NewGuid(), Guid.NewGuid(), Token); Assert.Equal(0, Volatile.Read(ref count));
        await notifier.NotifyAsync(backend.Order, backend.Restaurant, Token); await received.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        backend.FailAccess = true; await notifier.NotifyAsync(backend.Order, backend.Restaurant, Token);
        Assert.Single(factory.Services.GetRequiredService<OrderConnections>().All); Assert.Equal(1, Volatile.Read(ref count));
        backend.FailAccess = false;
        backend.Access = backend.Access with { Stamp = "revoked" }; await notifier.NotifyAsync(backend.Order, backend.Restaurant, Token);
        Assert.Empty(factory.Services.GetRequiredService<OrderConnections>().All); Assert.Equal(1, Volatile.Read(ref count));
    }
    [Theory]
    [InlineData(StaffRoles.Waiter)] [InlineData(StaffRoles.Customer)]
    public async Task NonKitchenRolesCannotOpenDedicatedKitchen(string role)
    {
        await using var factory = new AuthenticationTests.AuthFactory(); using var client = AuthenticationTests.Client(factory);
        await AuthenticationTests.SeedAsync(factory, role); using var login = await AuthenticationTests.LoginAsync(client);
        using var response = await client.GetAsync("/Kitchen", Token);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode); Assert.Equal("/Account/AccessDenied", new Uri(client.BaseAddress!, response.Headers.Location!).AbsolutePath);
    }
    [Fact]
    public async Task CustomerCannotReadStaffQueue()
    {
        await using var factory = new AuthenticationTests.AuthFactory { Operations = new Backend() }; using var client = AuthenticationTests.Client(factory);
        await AuthenticationTests.SeedAsync(factory, StaffRoles.Customer); using var login = await AuthenticationTests.LoginAsync(client);
        using var response = await client.GetAsync("/api/operations/queue", Token);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }
    [Fact]
    public async Task HubRenewInvocationsAreBounded()
    {
        var backend = new Backend(); await using var factory = new AuthenticationTests.AuthFactory { Operations = backend }; await using var hub = Hub(factory);
        await hub.StartAsync(Token); await hub.InvokeAsync("SubscribeReceipt", backend.Order, Backend.Secret, Token);
        for (var i = 0; i < 19; i++) await hub.InvokeAsync("Renew", Token);
        await Assert.ThrowsAsync<HubException>(() => hub.InvokeAsync("Renew", Token));
    }
    private static HubConnection Hub(AuthenticationTests.AuthFactory factory, string? cookie = null) => new HubConnectionBuilder().WithUrl("http://localhost/hubs/orders", options =>
    {
        options.Transports = HttpTransportType.LongPolling; options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
        if (cookie is not null) options.Headers["Cookie"] = cookie;
    }).Build();
    private sealed class Backend : IOperationsService
    {
        public static string Secret => new string('a', 64);
        public Guid Order { get; } = Guid.NewGuid(); public Guid Restaurant { get; } = Guid.NewGuid(); public Guid Actor { get; private set; }
        public bool FailAccess { get; set; } public StaffAccess? Access { get; set; } public OperationResult ChangeResult { get; init; } = OperationResult.Success();
        public Task<StaffAccess?> AccessAsync(Guid actor, CancellationToken token) => FailAccess ? Task.FromException<StaffAccess?>(new InvalidOperationException("Test-only access-check outage")) : Task.FromResult(Access?.ActorId == actor ? Access : null);
        public Task<ManagementRead<OrderQueue>> QueueAsync(Guid actor, CancellationToken token) { Actor = actor; return Task.FromResult(new ManagementRead<OrderQueue>(true, new([], 0))); }
        public Task<OperationResult> ChangeAsync(Guid actor, Guid orderId, StatusChange request, CancellationToken token) { Actor = actor; return Task.FromResult(ChangeResult); }
        public Task<bool> CanReadReceiptAsync(Guid orderId, string hash, CancellationToken token) => Task.FromResult(orderId == Order && hash == ServeFlow.Infrastructure.Ordering.OrderingService.Hash(Secret));
    }
}
