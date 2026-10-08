using System.Net;
using System.Net.Http.Json;
using ServeFlow.Application.Ordering;
using ServeFlow.Application.Security;
using ServeFlow.Domain.Enums;

namespace ServeFlow.IntegrationTests;

public sealed class OrderingHttpTests
{
    [Theory]
    [InlineData("/Dining/Start")] [InlineData("/Dining")] [InlineData("/Dining/Receipt/11111111-1111-1111-1111-111111111111")]
    public async Task CustomerPagesAreAnonymousAndNeverCached(string path)
    {
        await using var factory = new AuthenticationTests.AuthFactory(); using var client = AuthenticationTests.Client(factory);
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.NoStore);
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
    }
    [Fact]
    public async Task SessionExchangeRequiresAntiforgery()
    {
        await using var factory = new AuthenticationTests.AuthFactory { Ordering = new Backend() }; using var client = AuthenticationTests.Client(factory);
        using var response = await client.PostAsJsonAsync("/api/dining/session", new Exchange(Backend.Secret), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task ProtectedSessionCarriesServerResolvedTableAndCannotBeForged()
    {
        var backend = new Backend(); await using var factory = new AuthenticationTests.AuthFactory { Ordering = backend }; using var client = AuthenticationTests.Client(factory);
        await OpenAsync(client);
        using var response = await client.GetAsync("/api/dining/menu", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal(backend.Access, backend.SeenAccess);
        using var forged = AuthenticationTests.Client(factory); forged.DefaultRequestHeaders.Add("Cookie", "ServeFlow.Table=forged");
        using var rejected = await forged.GetAsync("/api/dining/menu", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
    }
    [Fact]
    public async Task AnonymousOrderingRequiresTableSessionEvenWithValidCsrf()
    {
        await using var factory = new AuthenticationTests.AuthFactory { Ordering = new Backend() }; using var client = AuthenticationTests.Client(factory);
        await CsrfAsync(client);
        using var response = await client.PostAsJsonAsync("/api/dining/orders", Request(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
    [Fact]
    public async Task OrderEndpointUsesProtectedTableAndReturnsCreatedThenReplay()
    {
        var backend = new Backend(); await using var factory = new AuthenticationTests.AuthFactory { Ordering = backend }; using var client = AuthenticationTests.Client(factory);
        await OpenAsync(client);
        var request = Request(); using var created = await client.PostAsJsonAsync("/api/dining/orders", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode); Assert.Equal(backend.Access, backend.SeenAccess);
        backend.Replayed = true; using var retry = await client.PostAsJsonAsync("/api/dining/orders", request, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }
    [Fact]
    public async Task ReceiptRequiresPrivateCapabilityHeader()
    {
        var backend = new Backend(); await using var factory = new AuthenticationTests.AuthFactory { Ordering = backend }; using var client = AuthenticationTests.Client(factory);
        using var denied = await client.GetAsync($"/api/dining/receipts/{backend.OrderId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
        client.DefaultRequestHeaders.Add("X-Receipt-Token", Backend.Secret);
        using var allowed = await client.GetAsync($"/api/dining/receipts/{backend.OrderId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        using var recovered = await client.GetAsync("/api/dining/receipts/recover", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, recovered.StatusCode);
    }
    [Theory]
    [InlineData(StaffRoles.Customer)] [InlineData(StaffRoles.KitchenStaff)]
    public async Task NonDiningStaffCannotCreateStaffOrders(string role)
    {
        await using var factory = new AuthenticationTests.AuthFactory { Ordering = new Backend() }; using var client = AuthenticationTests.Client(factory);
        await AuthenticationTests.SeedAsync(factory, role); using var login = await AuthenticationTests.LoginAsync(client);
        using var response = await client.GetAsync($"/WaiterOrder/Index/{Guid.NewGuid()}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode); Assert.Equal("/Account/AccessDenied", new Uri(client.BaseAddress!, response.Headers.Location!).AbsolutePath);
    }
    [Fact]
    public async Task AdministratorDownloadsAnActualSvgQrCodeViaPost()
    {
        var backend = new Backend(); await using var factory = new AuthenticationTests.AuthFactory { Ordering = backend }; using var client = AuthenticationTests.Client(factory);
        await AuthenticationTests.SeedAsync(factory, StaffRoles.Administrator); using var login = await AuthenticationTests.LoginAsync(client);
        var token = await AuthenticationTests.GetTokenAsync(client, "/Account/ChangePassword");
        using var response = await client.PostAsync($"/TableQr/Generate/{backend.Access.TableId}", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["version"] = Convert.ToBase64String(new byte[8]), ["__RequestVerificationToken"] = token }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.Equal("image/svg+xml", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("<svg", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)); Assert.NotEqual(Guid.Empty, backend.Actor);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
    }
    [Fact]
    public async Task WaiterSubmitsUsingServerIdentityAndRouteTable()
    {
        var backend = new Backend(); await using var factory = new AuthenticationTests.AuthFactory { Ordering = backend }; using var client = AuthenticationTests.Client(factory);
        var actor = await AuthenticationTests.SeedAsync(factory, StaffRoles.Waiter); using var login = await AuthenticationTests.LoginAsync(client);
        await CsrfAsync(client);
        using var response = await client.PostAsJsonAsync($"/api/staff/tables/{backend.Access.TableId}/orders", Request(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode); Assert.Equal(actor.Id, backend.Actor);
    }
    [Fact]
    public async Task OrderingWithTableCookieStillRequiresAntiforgery()
    {
        await using var factory = new AuthenticationTests.AuthFactory { Ordering = new Backend() }; using var client = AuthenticationTests.Client(factory);
        await OpenAsync(client); client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        using var response = await client.PostAsJsonAsync("/api/dining/orders", Request(), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
    [Fact]
    public async Task RepeatedSessionExchangeIsRateLimited()
    {
        await using var factory = new AuthenticationTests.AuthFactory { Ordering = new Backend() }; using var client = AuthenticationTests.Client(factory);
        await CsrfAsync(client);
        for (var i = 0; i < 10; i++) { using var allowed = await client.PostAsJsonAsync("/api/dining/session", new Exchange(Backend.Secret), TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, allowed.StatusCode); }
        using var limited = await client.PostAsJsonAsync("/api/dining/session", new Exchange(Backend.Secret), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }
    private static PlaceOrderRequest Request() => new(Guid.NewGuid().ToString(), Backend.Secret, null, []);
    private static async Task CsrfAsync(HttpClient client)
    {
        var token = await AuthenticationTests.GetTokenAsync(client, "/Dining/Start"); client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN"); client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token);
    }
    private static async Task OpenAsync(HttpClient client)
    {
        await CsrfAsync(client); using var response = await client.PostAsJsonAsync("/api/dining/session", new Exchange(Backend.Secret), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
    private sealed record Exchange(string Token);
    private sealed class Backend : IOrderingService
    {
        public const string Secret = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        public TableAccess Access { get; } = new(Guid.NewGuid(), new string('b', 64)); public TableAccess? SeenAccess { get; private set; }
        public Guid OrderId { get; } = Guid.NewGuid(); public Guid Actor { get; private set; } public bool Replayed { get; set; }
        public Task<TableAccess?> ExchangeAsync(string token, CancellationToken cancellationToken) => Task.FromResult<TableAccess?>(token == Secret ? Access : null);
        public Task<TableMenu?> MenuAsync(TableAccess access, CancellationToken cancellationToken) { SeenAccess = access; return Task.FromResult<TableMenu?>(new(access.TableId, "Test", 1, "EUR", [])); }
        public Task<TableMenu?> StaffMenuAsync(Guid actor, Guid tableId, CancellationToken cancellationToken) => MenuAsync(Access, cancellationToken);
        public Task<OrderingResult> PlaceAsync(TableAccess access, PlaceOrderRequest request, CancellationToken cancellationToken) { SeenAccess = access; return Task.FromResult(new OrderingResult(new(OrderId, "SF-TEST", 12m, "EUR", Replayed), null)); }
        public Task<OrderingResult> PlaceStaffAsync(Guid actor, Guid tableId, PlaceOrderRequest request, CancellationToken cancellationToken) { Actor = actor; return PlaceAsync(Access, request, cancellationToken); }
        public Task<OrderReceipt?> ReceiptAsync(Guid orderId, string token, CancellationToken cancellationToken) => Task.FromResult<OrderReceipt?>(token == Secret && (orderId == OrderId || orderId == Guid.Empty) ? new(OrderId, "SF-TEST", "Test", 1, "EUR", 12m, OrderStatus.Pending, DateTimeOffset.UtcNow, null, []) : null);
        public Task<string?> ImageAsync(TableAccess access, Guid itemId, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<QrIssued?> RotateQrAsync(Guid actor, Guid tableId, string version, CancellationToken cancellationToken) { Actor = actor; return Task.FromResult<QrIssued?>(new(Secret, 1)); }
    }
}
