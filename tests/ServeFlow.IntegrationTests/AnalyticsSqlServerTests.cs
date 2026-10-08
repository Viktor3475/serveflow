using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ServeFlow.Application;
using ServeFlow.Application.Accounts;
using ServeFlow.Application.Management;
using ServeFlow.Domain.Enums;
using ServeFlow.Application.Security;
using ServeFlow.Domain.Entities;
using ServeFlow.Infrastructure;
using ServeFlow.Infrastructure.Identity;
using ServeFlow.Infrastructure.Persistence;
using ServeFlow.Application.Analytics;
using ServeFlow.Application.Ordering;
using ServeFlow.Infrastructure.Ordering;

namespace ServeFlow.IntegrationTests;

public sealed class AnalyticsSqlServerTests : IAsyncLifetime
{
    public static bool SqlServerConfigured => SqlServerDatabaseTests.SqlServerConfigured;
    private const string Password = "Test-only-Strong42!";
    private readonly Guid _restaurantId = Guid.NewGuid();
    private ServiceProvider? _provider;
    private string? _connection;
    private readonly string _images = Path.Combine(Path.GetTempPath(), $"ServeFlowImages_{Guid.NewGuid():N}");
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        if (!SqlServerConfigured) return;
        _connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SERVEFLOW_TEST_SQLSERVER"))
            { InitialCatalog = $"ServeFlowTests_{Guid.NewGuid():N}", Pooling = false }.ConnectionString;
        await using (var db = Context()) { await db.Database.MigrateAsync(TestContext.Current.CancellationToken); }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ServeFlow"] = _connection, ["Uploads:RootPath"] = _images,
            ["Bootstrap:RestaurantId"] = _restaurantId.ToString(), ["Bootstrap:RestaurantName"] = "Identity test restaurant",
            ["Bootstrap:Email"] = "root@example.test", ["Bootstrap:DisplayName"] = "Administrator", ["Bootstrap:Password"] = Password
        }).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddDataProtection().UseEphemeralDataProtectionProvider();
        services.AddSingleton<TimeProvider>(new FixedClock());
        services.AddApplication();
        services.AddInfrastructure(configuration, new TestEnvironment());
        _provider = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await using var db = Context();
            await db.Database.EnsureDeletedAsync(CancellationToken.None);
        }
        if (_provider is not null) await _provider.DisposeAsync();
        if (Directory.Exists(_images)) Directory.Delete(_images, recursive: true);
    }




    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real analytics aggregation tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task CollectionsUseOnlySuccessfulPaymentsAndDoNotDuplicateOrdersOrMixCurrencies()
    {
        var setup = await SetupAsync();
        await using var scope = _provider!.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        var table = await db.RestaurantTables.SingleAsync(Token); var item = await db.MenuItems.SingleAsync(Token);
        var first = SeedOrder(db, table, item, OrderStatus.Completed, Utc(8, 10));
        var cancelled = SeedOrder(db, table, item, OrderStatus.Cancelled, Utc(8, 11));
        SeedOrder(db, table, item, OrderStatus.Pending, Utc(8, 12));
        var dollars = SeedOrder(db, table, item, OrderStatus.Completed, Utc(8, 12), "USD");
        Pay(db, first, 7m, PaymentStatus.Succeeded, Utc(8, 10)); Pay(db, first, 5m, PaymentStatus.Succeeded, Utc(8, 11));
        Pay(db, first, 8m, PaymentStatus.Pending); Pay(db, first, 100m, PaymentStatus.Failed);
        Pay(db, cancelled, 4m, PaymentStatus.Succeeded, Utc(8, 11)); Pay(db, dollars, 20m, PaymentStatus.Succeeded, Utc(8, 12));
        item.Price = 999m; item.Name = "Renamed soup"; await db.SaveChangesAsync(Token);
        var result = await scope.ServiceProvider.GetRequiredService<IAnalyticsService>().ReadAsync(setup.Actor, new(new(2026, 10, 8), new(2026, 10, 8)), Token);
        Assert.True(result.Allowed); var data = result.Data!;
        Assert.Equal(16m, data.Revenue); Assert.Equal(16m, data.TodayRevenue); Assert.Equal(3, data.OrderCount); Assert.Equal(1, data.CancelledCount); Assert.Equal(20m, data.AverageOrderValue);
        Assert.Equal(8, Assert.Single(data.Popular).Units); Assert.Equal("Renamed soup", data.Popular[0].Name);
        Assert.Equal(12m, Assert.Single(data.Recent, x => x.Id == first.Id).Collected);
    }
    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real analytics aggregation tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task PaidDateLocalMidnightIsInclusiveAndNextMidnightIsExclusive()
    {
        var setup = await SetupAsync();
        await using var scope = _provider!.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        var order = SeedOrder(db, await db.RestaurantTables.SingleAsync(Token), await db.MenuItems.SingleAsync(Token), OrderStatus.Completed, Utc(7, 12));
        Pay(db, order, 1m, PaymentStatus.Succeeded, Utc(7, 20).AddMinutes(59).AddSeconds(59));
        Pay(db, order, 2m, PaymentStatus.Succeeded, Utc(7, 21));
        Pay(db, order, 3m, PaymentStatus.Succeeded, Utc(8, 20).AddMinutes(59).AddSeconds(59));
        Pay(db, order, 4m, PaymentStatus.Succeeded, Utc(8, 21)); await db.SaveChangesAsync(Token);
        var data = (await scope.ServiceProvider.GetRequiredService<IAnalyticsService>().ReadAsync(setup.Actor, new(new(2026, 10, 8), new(2026, 10, 8)), Token)).Data!;
        Assert.Equal(5m, data.Revenue); Assert.Equal(0, data.OrderCount); Assert.Equal(5m, Assert.Single(data.Days).Revenue); Assert.Empty(data.Recent);
    }
    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real analytics aggregation tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task EmptyDataProducesZeroMetricsAndOccupancyExcludesArchivedTables()
    {
        var setup = await SetupAsync();
        await using var scope = _provider!.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        db.AddRange(new RestaurantTable { RestaurantId = _restaurantId, Number = 2, Capacity = 4, Status = TableStatus.Occupied },
            new RestaurantTable { RestaurantId = _restaurantId, Number = 3, Capacity = 4, Status = TableStatus.Reserved },
            new RestaurantTable { RestaurantId = _restaurantId, Number = 4, Capacity = 4, Status = TableStatus.Occupied, IsArchived = true });
        await db.SaveChangesAsync(Token);
        var data = (await scope.ServiceProvider.GetRequiredService<IAnalyticsService>().ReadAsync(setup.Actor, new(null, null), Token)).Data!;
        Assert.Equal(0m, data.Revenue); Assert.Equal(0m, data.AverageOrderValue); Assert.Equal(0, data.OrderCount); Assert.Equal(7, data.Days.Count);
        Assert.All(data.Days, x => Assert.Equal(0m, x.Revenue)); Assert.Equal(3, data.Tables.Total); Assert.Equal(33.3m, data.Tables.Percent);
    }
    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real analytics aggregation tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task AdministratorScopeExcludesOtherRestaurantsAndNonAdministratorsAreRejected()
    {
        var setup = await SetupAsync();
        await using var scope = _provider!.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        var restaurant = new Restaurant { Name = "Outside" }; var category = new MenuCategory { RestaurantId = restaurant.Id, Name = "Outside" };
        var item = new MenuItem { RestaurantId = restaurant.Id, MenuCategoryId = category.Id, Name = "Outside", Price = 5m };
        var table = new RestaurantTable { RestaurantId = restaurant.Id, Number = 1, Capacity = 4 };
        db.AddRange(restaurant, category, item, table); var outside = SeedOrder(db, table, item, OrderStatus.Completed, Utc(8, 12)); Pay(db, outside, 20m, PaymentStatus.Succeeded, Utc(8, 12));
        await db.SaveChangesAsync(Token);
        var analytics = scope.ServiceProvider.GetRequiredService<IAnalyticsService>();
        var own = (await analytics.ReadAsync(setup.Actor, new(null, null), Token)).Data!; Assert.Equal(0m, own.Revenue); Assert.Equal(0, own.OrderCount); Assert.Empty(own.Popular);
        Assert.True((await scope.ServiceProvider.GetRequiredService<IEmployeeService>().CreateAsync(setup.Actor, new("waiter@example.test", "Waiter", Password, StaffRoles.Waiter), Token)).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); var waiter = (await users.FindByEmailAsync("waiter@example.test"))!;
        Assert.False((await analytics.ReadAsync(waiter.Id, new(null, null), Token)).Allowed);
        var root = (await users.FindByIdAsync(setup.Actor.ToString()))!; root.IsArchived = true; Assert.True((await users.UpdateAsync(root)).Succeeded);
        Assert.False((await analytics.ReadAsync(root.Id, new(null, null), Token)).Allowed);
    }
    private static DateTimeOffset Utc(int day, int hour) => new(2026, 10, day, hour, 0, 0, TimeSpan.Zero);
    private static Order SeedOrder(ServeFlowDbContext db, RestaurantTable table, MenuItem item, OrderStatus status, DateTimeOffset created, string currency = "EUR")
    {
        var order = new Order { RestaurantId = table.RestaurantId, RestaurantTableId = table.Id, OrderNumber = "SA-" + Guid.NewGuid().ToString("N")[..20],
            IdempotencyKey = Guid.NewGuid().ToString(), RequestFingerprint = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            ReceiptTokenHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), CurrencyCode = currency, Total = 20m, Status = status, CreatedAt = created };
        order.Items.Add(new OrderItem { RestaurantId = table.RestaurantId, OrderId = order.Id, MenuItemId = item.Id, NameSnapshot = "Original soup", UnitPriceSnapshot = 5m, Quantity = 4 });
        db.Add(order); return order;
    }
    private static void Pay(ServeFlowDbContext db, Order order, decimal amount, PaymentStatus status, DateTimeOffset? paidAt = null) => db.Add(new Payment
        { RestaurantId = order.RestaurantId, OrderId = order.Id, Amount = amount, CurrencyCode = order.CurrencyCode, Method = PaymentMethod.Cash, Status = status, PaidAt = paidAt });
    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Utc(8, 12); }
    private async Task<(Guid Actor, TableAccess Access, TableMenu Menu)> SetupAsync()
    {
        var actor = await BootstrapAsync(); await using var scope = _provider!.CreateAsyncScope();
        var management = scope.ServiceProvider.GetRequiredService<IRestaurantManagementService>();
        Assert.True((await management.SaveCategoryAsync(actor.Id, new(null, "Lunch", 0, false, null), Token)).Succeeded);
        var category = Assert.Single((await management.CategoriesAsync(actor.Id, 1, null, false, Token)).Value!.Items);
        Assert.True((await management.SaveMenuAsync(actor.Id, new(null, category.Id, "Soup", "Soup description", 8.50m, true, false, null), Token)).Succeeded);
        var product = Assert.Single((await management.MenuAsync(actor.Id, 1, null, false, Token)).Value!.Items);
        Assert.True((await scope.ServiceProvider.GetRequiredService<IMenuOptionService>().SaveAsync(actor.Id, new(null, product.Id, "Bread", 1.25m, true, false, null), Token)).Succeeded);
        Assert.True((await management.SaveTableAsync(actor.Id, new(null, 1, 4, TableStatus.Available, false, null), Token)).Succeeded);
        var table = Assert.Single((await management.TablesAsync(actor.Id, 1, false, Token)).Value!.Items);
        var ordering = scope.ServiceProvider.GetRequiredService<IOrderingService>();
        var issued = await ordering.RotateQrAsync(actor.Id, table.Id, table.Version, Token); Assert.NotNull(issued);
        var access = await ordering.ExchangeAsync(issued.Token, Token); Assert.NotNull(access);
        return (actor.Id, access, (await ordering.MenuAsync(access, Token))!);
    }
    private static PlaceOrderRequest Request(TableMenu menu) => new(Guid.NewGuid().ToString(), Convert.ToHexStringLower(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32)), "Test notes",
        [new(menu.Items[0].Id, menu.Items[0].Version, 2, "No salt", [new(menu.Items[0].Options[0].Id, menu.Items[0].Options[0].Version)])]);
    private async Task<ApplicationUser> BootstrapAsync()
    {
        await using var scope = _provider!.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AdministratorProvisioner>().ProvisionAsync(TestContext.Current.CancellationToken);
        return (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("root@example.test"))!;
    }

    private ServeFlowDbContext Context() => new(new DbContextOptionsBuilder<ServeFlowDbContext>().UseSqlServer(_connection!).Options);

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "ServeFlow.Tests";
        public string ContentRootPath { get; set; } = "/tmp";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
