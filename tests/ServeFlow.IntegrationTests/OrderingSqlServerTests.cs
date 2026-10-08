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
using ServeFlow.Application.Ordering;
using ServeFlow.Infrastructure.Ordering;

namespace ServeFlow.IntegrationTests;

public sealed class OrderingSqlServerTests : IAsyncLifetime
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



    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real ordering transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task OrderSnapshotsPricesOptionsHistoryAndOccupiedTableAtomically()
    {
        var setup = await SetupAsync();
        await using var scope = _provider!.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IOrderingService>();
        var request = Request(setup.Menu);
        var result = await service.PlaceAsync(setup.Access, request, Token);
        Assert.NotNull(result.Order); Assert.Equal(19.50m, result.Order.Total);
        var db = scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        var product = await db.MenuItems.SingleAsync(Token); product.Name = "New name"; product.Price = 99m;
        var option = await db.MenuItemOptions.SingleAsync(Token); option.Name = "New option"; option.AdditionalPrice = 10m;
        await db.SaveChangesAsync(Token);
        var receipt = (await service.ReceiptAsync(result.Order.Id, request.ReceiptToken, Token))!;
        var line = Assert.Single(receipt.Lines); Assert.Equal("Soup", line.Name); Assert.Equal(8.50m, line.Price);
        Assert.Equal("Bread", Assert.Single(line.Options).Name); Assert.Equal(1.25m, line.Options[0].Price);
        Assert.Equal(19.50m, receipt.Total);
        Assert.Equal(TableStatus.Occupied, (await db.RestaurantTables.AsNoTracking().SingleAsync(Token)).Status);
        Assert.Equal(OrderStatus.Pending, (await db.OrderStatusHistory.AsNoTracking().SingleAsync(Token)).NewStatus);
        Assert.Null(await service.ReceiptAsync(result.Order.Id, new string('f', 64), Token));
        Assert.Equal(result.Order.Id, (await service.ReceiptAsync(Guid.Empty, request.ReceiptToken, Token))!.Id);
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real ordering transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task SimultaneousIdenticalSubmissionsCreateOneOrderAndReturnSameReceipt()
    {
        var setup = await SetupAsync(); var request = Request(setup.Menu);
        await using var first = _provider!.CreateAsyncScope(); await using var second = _provider!.CreateAsyncScope();
        var results = await Task.WhenAll(first.ServiceProvider.GetRequiredService<IOrderingService>().PlaceAsync(setup.Access, request, Token),
            second.ServiceProvider.GetRequiredService<IOrderingService>().PlaceAsync(setup.Access, request, Token));
        Assert.All(results, x => Assert.NotNull(x.Order));
        Assert.Equal(results[0].Order!.Id, results[1].Order!.Id);
        Assert.Single(results, x => x.Order!.Replayed);
        await using var read = _provider!.CreateAsyncScope();
        Assert.Equal(1, await read.ServiceProvider.GetRequiredService<ServeFlowDbContext>().Orders.CountAsync(Token));
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real ordering transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task ReusedSubmissionKeyWithChangedContentReturnsConflict()
    {
        var setup = await SetupAsync(); var request = Request(setup.Menu);
        await using var scope = _provider!.CreateAsyncScope(); var service = scope.ServiceProvider.GetRequiredService<IOrderingService>();
        Assert.NotNull((await service.PlaceAsync(setup.Access, request, Token)).Order);
        Assert.True((await service.PlaceAsync(setup.Access, request with { Notes = "Different" }, Token)).Conflict);
        Assert.True((await service.PlaceAsync(setup.Access, request, Token)).Order!.Replayed);
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real ordering transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task WrongTableAndRotatedQrCapabilitiesCannotSubmitOrders()
    {
        var setup = await SetupAsync();
        await using var scope = _provider!.CreateAsyncScope(); var service = scope.ServiceProvider.GetRequiredService<IOrderingService>();
        var db = scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        var outside = new Restaurant { Name = "Outside" };
        var outsideTable = new RestaurantTable { RestaurantId = outside.Id, Number = 1, Capacity = 4, QrTokenHash = new string('f', 64) };
        db.AddRange(outside, outsideTable); await db.SaveChangesAsync(Token);
        Assert.True((await service.PlaceAsync(setup.Access with { TableId = outsideTable.Id }, Request(setup.Menu), Token)).Forbidden);
        Assert.True((await service.PlaceStaffAsync(setup.Actor, outsideTable.Id, Request(setup.Menu), Token)).Forbidden);
        var table = (await scope.ServiceProvider.GetRequiredService<IRestaurantManagementService>().GetTableAsync(setup.Actor, setup.Access.TableId, Token)).Value!;
        var qr = await service.RotateQrAsync(setup.Actor, table.Id, table.Version, Token);
        Assert.NotNull(qr); Assert.NotEqual(setup.Access.TokenHash, OrderingService.Hash(qr.Token));
        Assert.Null(await service.MenuAsync(setup.Access, Token));
        Assert.True((await service.PlaceAsync(setup.Access, Request(setup.Menu), Token)).Forbidden);
        Assert.NotNull(await service.ExchangeAsync(qr.Token, Token));
        Assert.Null(await service.ExchangeAsync("guessable", Token));
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real ordering transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task UnavailableProductsAndOptionsAreRejectedWithoutCreatingOrders()
    {
        var setup = await SetupAsync();
        await using var scope = _provider!.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        var option = await db.MenuItemOptions.SingleAsync(Token); option.IsAvailable = false; await db.SaveChangesAsync(Token);
        var service = scope.ServiceProvider.GetRequiredService<IOrderingService>();
        Assert.True((await service.PlaceAsync(setup.Access, Request(setup.Menu), Token)).Conflict);
        var product = await db.MenuItems.SingleAsync(Token); product.IsAvailable = false; await db.SaveChangesAsync(Token);
        Assert.True((await service.PlaceAsync(setup.Access, Request(setup.Menu), Token)).Conflict);
        Assert.Equal(0, await db.Orders.CountAsync(Token));
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real ordering transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task ReservedTablesBlockCustomersAndWaitersHaveScopedCreationRights()
    {
        var setup = await SetupAsync();
        await using var scope = _provider!.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        var table = await db.RestaurantTables.SingleAsync(Token); table.Status = TableStatus.Reserved; await db.SaveChangesAsync(Token);
        var service = scope.ServiceProvider.GetRequiredService<IOrderingService>();
        Assert.True((await service.PlaceAsync(setup.Access, Request(setup.Menu), Token)).Conflict);
        var employees = scope.ServiceProvider.GetRequiredService<IEmployeeService>();
        Assert.True((await employees.CreateAsync(setup.Actor, new("waiter@example.test", "Waiter", Password, StaffRoles.Waiter), Token)).Succeeded);
        var waiter = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("waiter@example.test"))!;
        var result = await service.PlaceStaffAsync(waiter.Id, table.Id, Request(setup.Menu), Token);
        Assert.NotNull(result.Order);
        Assert.Equal(waiter.Id, (await db.Orders.AsNoTracking().SingleAsync(Token)).CreatedByUserId);
        Assert.True((await service.PlaceStaffAsync(waiter.Id, Guid.NewGuid(), Request(setup.Menu), Token)).Forbidden);
        Assert.True((await employees.CreateAsync(setup.Actor, new("kitchen@example.test", "Kitchen", Password, StaffRoles.KitchenStaff), Token)).Succeeded);
        var kitchen = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("kitchen@example.test"))!;
        Assert.True((await service.PlaceStaffAsync(kitchen.Id, table.Id, Request(setup.Menu), Token)).Forbidden);
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real ordering transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task CustomerOrderLimitIsTransactionalAndDoesNotBlockIdempotentRetries()
    {
        var setup = await SetupAsync();
        await using var scope = _provider!.CreateAsyncScope(); var service = scope.ServiceProvider.GetRequiredService<IOrderingService>();
        PlaceOrderRequest? last = null;
        for (var i = 0; i < 10; i++) { last = Request(setup.Menu); Assert.NotNull((await service.PlaceAsync(setup.Access, last, Token)).Order); }
        Assert.True((await service.PlaceAsync(setup.Access, Request(setup.Menu), Token)).Throttled);
        Assert.True((await service.PlaceAsync(setup.Access, last!, Token)).Order!.Replayed);
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
