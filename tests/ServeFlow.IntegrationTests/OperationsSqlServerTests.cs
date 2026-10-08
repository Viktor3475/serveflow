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
using ServeFlow.Application.Operations;
using ServeFlow.Application.Ordering;
using ServeFlow.Infrastructure.Ordering;

namespace ServeFlow.IntegrationTests;

public sealed class OperationsSqlServerTests : IAsyncLifetime
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




    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real order workflow transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task ValidLifecycleAppendsHistoryAndReleasesTheLastOrderTable()
    {
        var setup = await SetupAsync(); var order = await CreateAsync(setup.Access, setup.Menu);
        foreach (var status in new[] { OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Ready, OrderStatus.Served, OrderStatus.Completed })
            Assert.True((await ChangeAsync(setup.Actor, order.Id, status, await VersionAsync(order.Id))).Succeeded);
        await using var scope = _provider!.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        var history = await db.OrderStatusHistory.Where(x => x.OrderId == order.Id).OrderBy(x => x.ChangedAt).ToListAsync(Token);
        Assert.Equal(6, history.Count); Assert.Equal(OrderStatus.Pending, history[0].NewStatus); Assert.Equal(OrderStatus.Completed, history[^1].NewStatus);
        Assert.All(history.Skip(1), x => Assert.Equal(setup.Actor, x.ChangedByUserId));
        Assert.Equal(TableStatus.Available, (await db.RestaurantTables.SingleAsync(Token)).Status);
        Assert.Empty((await scope.ServiceProvider.GetRequiredService<IOperationsService>().QueueAsync(setup.Actor, Token)).Value!.Orders);
        Assert.False((await ChangeAsync(setup.Actor, order.Id, OrderStatus.Cancelled, await VersionAsync(order.Id), "Too late")).Succeeded);
    }
    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real order workflow transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task CancellationRequiresReasonAndKeepsTableOccupiedForAnotherActiveOrder()
    {
        var setup = await SetupAsync(); var first = await CreateAsync(setup.Access, setup.Menu); var second = await CreateAsync(setup.Access, setup.Menu);
        Assert.False((await ChangeAsync(setup.Actor, first.Id, OrderStatus.Cancelled, await VersionAsync(first.Id))).Succeeded);
        Assert.True((await ChangeAsync(setup.Actor, first.Id, OrderStatus.Cancelled, await VersionAsync(first.Id), "Guest changed plans")).Succeeded);
        await using var scope = _provider!.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        Assert.Equal(TableStatus.Occupied, (await db.RestaurantTables.AsNoTracking().SingleAsync(Token)).Status);
        Assert.True((await ChangeAsync(setup.Actor, second.Id, OrderStatus.Cancelled, await VersionAsync(second.Id), "Guest left")).Succeeded);
        Assert.Equal(TableStatus.Available, (await db.RestaurantTables.AsNoTracking().SingleAsync(Token)).Status);
        Assert.Equal("Guest changed plans", (await db.OrderStatusHistory.AsNoTracking().SingleAsync(x => x.OrderId == first.Id && x.NewStatus == OrderStatus.Cancelled, Token)).Reason);
    }
    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real order workflow transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task ConcurrentAcceptanceCommitsExactlyOneHistoryChange()
    {
        var setup = await SetupAsync(); var order = await CreateAsync(setup.Access, setup.Menu); var version = await VersionAsync(order.Id);
        var results = await Task.WhenAll(ChangeAsync(setup.Actor, order.Id, OrderStatus.Accepted, version), ChangeAsync(setup.Actor, order.Id, OrderStatus.Accepted, version));
        Assert.Single(results, x => x.Succeeded); Assert.Single(results, x => x.Conflict);
        await using var scope = _provider!.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        Assert.Equal(2, await db.OrderStatusHistory.CountAsync(x => x.OrderId == order.Id, Token));
        Assert.Equal(OrderStatus.Accepted, (await db.Orders.SingleAsync(Token)).Status);
    }
    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real order workflow transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task KitchenAndWaiterCannotUseEachOthersTransitions()
    {
        var setup = await SetupAsync(); var order = await CreateAsync(setup.Access, setup.Menu);
        await using var scope = _provider!.CreateAsyncScope(); var employees = scope.ServiceProvider.GetRequiredService<IEmployeeService>();
        Assert.True((await employees.CreateAsync(setup.Actor, new("waiter@example.test", "Waiter", Password, StaffRoles.Waiter), Token)).Succeeded);
        Assert.True((await employees.CreateAsync(setup.Actor, new("kitchen@example.test", "Kitchen", Password, StaffRoles.KitchenStaff), Token)).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var waiter = (await users.FindByEmailAsync("waiter@example.test"))!; var kitchen = (await users.FindByEmailAsync("kitchen@example.test"))!;
        Assert.False((await ChangeAsync(waiter.Id, order.Id, OrderStatus.Accepted, await VersionAsync(order.Id))).Succeeded);
        foreach (var status in new[] { OrderStatus.Accepted, OrderStatus.Preparing, OrderStatus.Ready }) Assert.True((await ChangeAsync(kitchen.Id, order.Id, status, await VersionAsync(order.Id))).Succeeded);
        Assert.False((await ChangeAsync(kitchen.Id, order.Id, OrderStatus.Served, await VersionAsync(order.Id))).Succeeded);
        Assert.True((await ChangeAsync(waiter.Id, order.Id, OrderStatus.Served, await VersionAsync(order.Id))).Succeeded);
    }
    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real order workflow transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task CrossRestaurantAndArchivedAccountsCannotReadQueueOrChangeOrders()
    {
        var setup = await SetupAsync(); var order = await CreateAsync(setup.Access, setup.Menu);
        await using var scope = _provider!.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        var outside = new Restaurant { Name = "Outside" }; db.Add(outside); await db.SaveChangesAsync(Token);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var outsider = new ApplicationUser { Id = Guid.NewGuid(), RestaurantId = outside.Id, Email = "outside@example.test", UserName = "outside@example.test", DisplayName = "Outside" };
        Assert.True((await users.CreateAsync(outsider, Password)).Succeeded); Assert.True((await users.AddToRoleAsync(outsider, StaffRoles.Administrator)).Succeeded);
        Assert.True((await ChangeAsync(outsider.Id, order.Id, OrderStatus.Accepted, await VersionAsync(order.Id))).Forbidden);
        Assert.Empty((await scope.ServiceProvider.GetRequiredService<IOperationsService>().QueueAsync(outsider.Id, Token)).Value!.Orders);
        outsider.IsArchived = true; Assert.True((await users.UpdateAsync(outsider)).Succeeded);
        Assert.False((await scope.ServiceProvider.GetRequiredService<IOperationsService>().QueueAsync(outsider.Id, Token)).Allowed);
    }
    private async Task<OrderCreated> CreateAsync(TableAccess access, TableMenu menu)
    {
        await using var scope = _provider!.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IOrderingService>().PlaceAsync(access, Request(menu), Token)).Order!;
    }
    private async Task<string> VersionAsync(Guid order)
    {
        await using var scope = _provider!.CreateAsyncScope();
        return Convert.ToBase64String((await scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>().Orders.AsNoTracking().SingleAsync(x => x.Id == order, Token)).Version);
    }
    private async Task<OperationResult> ChangeAsync(Guid actor, Guid order, OrderStatus status, string version, string? reason = null)
    {
        await using var scope = _provider!.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IOperationsService>().ChangeAsync(actor, order, new(status, version, reason), Token);
    }
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
