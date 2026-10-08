using System.Net;
using ServeFlow.Application.Analytics;
using ServeFlow.Application.Security;
using ServeFlow.Domain.Enums;

namespace ServeFlow.IntegrationTests;

public sealed class DashboardHttpTests
{
    [Fact]
    public async Task AnonymousDashboardRequiresLogin()
    {
        await using var factory = new AuthenticationTests.AuthFactory { Analytics = new Backend() }; using var client = AuthenticationTests.Client(factory);
        using var response = await client.GetAsync("/Dashboard", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }
    [Theory]
    [InlineData(StaffRoles.Waiter)] [InlineData(StaffRoles.KitchenStaff)] [InlineData(StaffRoles.Customer)]
    public async Task NonAdministratorDashboardIsDenied(string role)
    {
        await using var factory = new AuthenticationTests.AuthFactory { Analytics = new Backend() }; using var client = AuthenticationTests.Client(factory);
        await AuthenticationTests.SeedAsync(factory, role); using var login = await AuthenticationTests.LoginAsync(client);
        using var response = await client.GetAsync("/Dashboard", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/Account/AccessDenied", new Uri(client.BaseAddress!, response.Headers.Location!).AbsolutePath);
    }
    [Fact]
    public async Task AdministratorRendersAnalyticsAccessibleTablesAndScopedSidebar()
    {
        var backend = new Backend(); await using var factory = new AuthenticationTests.AuthFactory { Analytics = backend }; using var client = AuthenticationTests.Client(factory);
        var actor = await AuthenticationTests.SeedAsync(factory, StaffRoles.Administrator); using var login = await AuthenticationTests.LoginAsync(client);
        using var response = await client.GetAsync("/Dashboard", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.OK, response.StatusCode); Assert.True(response.Headers.CacheControl!.NoStore);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Collected in this range", html); Assert.Contains("View exact daily values", html); Assert.Contains("aria-current=\"page\"", html); Assert.Contains("/lib/chartjs/chart.umd.min.js", html); Assert.Equal(actor.Id, backend.Actor);
        var capture = Environment.GetEnvironmentVariable("SERVEFLOW_UI_CAPTURE_DIR");
        if (!string.IsNullOrWhiteSpace(capture)) { Directory.CreateDirectory(capture); await File.WriteAllTextAsync(Path.Combine(capture, "dashboard.html"), html, TestContext.Current.CancellationToken); }
    }
    [Fact]
    public async Task DateFiltersReachServiceAndMalformedDatesAreRejected()
    {
        var backend = new Backend(); await using var factory = new AuthenticationTests.AuthFactory { Analytics = backend }; using var client = AuthenticationTests.Client(factory);
        await AuthenticationTests.SeedAsync(factory, StaffRoles.Administrator); using var login = await AuthenticationTests.LoginAsync(client);
        using var selected = await client.GetAsync("/Dashboard?from=2026-10-01&to=2026-10-08&actor=ignored&restaurantId=ignored", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode); Assert.Equal(new DateOnly(2026, 10, 1), backend.Filter!.From); Assert.Equal(new DateOnly(2026, 10, 8), backend.Filter.To);
        using var invalid = await client.GetAsync("/Dashboard?from=not-a-date", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }
    [Fact]
    public async Task InvalidRangesRenderAnAccessibleError()
    {
        await using var factory = new AuthenticationTests.AuthFactory { Analytics = new Backend { Error = "Choose a shorter range." } }; using var client = AuthenticationTests.Client(factory);
        await AuthenticationTests.SeedAsync(factory, StaffRoles.Administrator); using var login = await AuthenticationTests.LoginAsync(client);
        using var response = await client.GetAsync("/Dashboard", TestContext.Current.CancellationToken); Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("role=\"alert\"", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task ProductNamesAreEncodedInHtmlAndChartData()
    {
        await using var factory = new AuthenticationTests.AuthFactory { Analytics = new Backend { ProductName = "<script>alert('test')</script>" } }; using var client = AuthenticationTests.Client(factory);
        await AuthenticationTests.SeedAsync(factory, StaffRoles.Administrator); using var login = await AuthenticationTests.LoginAsync(client);
        using var response = await client.GetAsync("/Dashboard", TestContext.Current.CancellationToken); var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.DoesNotContain("<script>alert(", html); Assert.Contains("&lt;script&gt;", html);
    }
    private sealed class Backend : IAnalyticsService
    {
        public Guid Actor { get; private set; } public AnalyticsFilter? Filter { get; private set; } public string? Error { get; init; } public string ProductName { get; init; } = "Seasonal risotto";
        public Task<DashboardResult> ReadAsync(Guid actor, AnalyticsFilter filter, CancellationToken token)
        {
            Actor = actor; Filter = filter;
            if (Error is not null) return Task.FromResult(new DashboardResult(true, null, Error));
            var from = new DateOnly(2026, 10, 2); var values = new[] { 215.50m, 326.00m, 184.75m, 419.20m, 287.60m, 342.80m, 385.20m };
            var days = values.Select((amount, i) => new DailyMetric(from.AddDays(i), amount, 12 + i * 3)).ToArray();
            var data = new DashboardData("Olive & Oak · UI fixture", "EUR", "Europe/Sofia", from, from.AddDays(6), new(2026, 10, 8, 18, 30, 0, TimeSpan.Zero), values.Sum(), 385.20m, 147, 3, 24.80m,
                new(8, 5, 3), days, [new(Guid.NewGuid(), ProductName, 48), new(Guid.NewGuid(), "Grilled sea bass", 36), new(Guid.NewGuid(), "Burrata salad", 29), new(Guid.NewGuid(), "Lemon tart", 24)],
                Enumerable.Range(0, 5).Select(i => new RecentOrder(Guid.NewGuid(), $"SF-UI-FIXTURE-00{i + 1}", i + 2, (OrderStatus)i, new(2026, 10, 8, 17, 45 - i * 7, 0, TimeSpan.Zero), 28.50m + i * 8, i == 4 ? 60.50m : 0m)).ToArray());
            return Task.FromResult(new DashboardResult(true, data));
        }
    }
}
