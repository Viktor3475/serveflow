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
using SkiaSharp;

namespace ServeFlow.IntegrationTests;

public sealed class ManagementSqlServerTests : IAsyncLifetime
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


    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real management transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task CategoryArchiveRequiresProductsToBeArchivedAndRestorationRequiresActiveCategory()
    {
        var actor = await BootstrapAsync();
        await using var scope = _provider!.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IRestaurantManagementService>();
        var category = await CategoryAsync(service, actor.Id);
        var product = await ProductAsync(service, actor.Id, category.Id);
        var archiveCategory = new CategoryInput(category.Id, category.Name, 0, true, category.Version);
        Assert.False((await service.SaveCategoryAsync(actor.Id, archiveCategory, Token)).Succeeded);
        Assert.True((await service.SaveMenuAsync(actor.Id, MenuInputFor(product, archived: true), Token)).Succeeded);
        Assert.True((await service.SaveCategoryAsync(actor.Id, archiveCategory, Token)).Succeeded);
        product = (await service.GetMenuAsync(actor.Id, product.Id, Token)).Value!;
        Assert.True(product.IsArchived);
        Assert.False(product.IsAvailable);
        Assert.False((await service.SaveMenuAsync(actor.Id, MenuInputFor(product), Token)).Succeeded);
        Assert.Empty((await service.CategoriesAsync(actor.Id, 1, null, false, Token)).Value!.Items);
        Assert.Single((await service.CategoriesAsync(actor.Id, 1, "Lunch", true, Token)).Value!.Items);
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real management transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task StaleProductAndSettingsEditsDoNotOverwriteNewerValues()
    {
        var actor = await BootstrapAsync();
        await using var scope = _provider!.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IRestaurantManagementService>();
        var product = await ProductAsync(service, actor.Id, (await CategoryAsync(service, actor.Id)).Id);
        Assert.True((await service.SaveMenuAsync(actor.Id, MenuInputFor(product) with { Price = 14.25m }, Token)).Succeeded);
        Assert.True((await service.SaveMenuAsync(actor.Id, MenuInputFor(product) with { Price = 1m }, Token)).Conflict);
        Assert.Equal(14.25m, (await service.GetMenuAsync(actor.Id, product.Id, Token)).Value!.Price);
        var settings = (await service.GetSettingsAsync(actor.Id, Token)).Value!;
        Assert.True((await service.SaveSettingsAsync(actor.Id, new("New name", "UTC", settings.Version), Token)).Succeeded);
        Assert.True((await service.SaveSettingsAsync(actor.Id, new("Stale name", "UTC", settings.Version), Token)).Conflict);
        var current = (await service.GetSettingsAsync(actor.Id, Token)).Value!;
        Assert.Equal("New name", current.Name);
        Assert.Equal(settings.CurrencyCode, current.CurrencyCode);
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real management transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task OtherRestaurantRecordsCannotBeReadEditedOrUsedAsCategories()
    {
        var actor = await BootstrapAsync();
        await using var scope = _provider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        var other = new Restaurant { Name = "Other" };
        var category = new MenuCategory { RestaurantId = other.Id, Name = "Outside" };
        db.AddRange(other, category);
        await db.SaveChangesAsync(Token);
        var version = Convert.ToBase64String(category.Version);
        var service = scope.ServiceProvider.GetRequiredService<IRestaurantManagementService>();
        Assert.Null((await service.GetCategoryAsync(actor.Id, category.Id, Token)).Value);
        Assert.True((await service.SaveCategoryAsync(actor.Id, new(category.Id, "Invalid", 0, false, version), Token)).Forbidden);
        Assert.False((await service.SaveMenuAsync(actor.Id, new(null, category.Id, "Invalid", "", 2m, true, false, null), Token)).Succeeded);
        Assert.Empty((await service.CategoriesAsync(actor.Id, 1, null, true, Token)).Value!.Items);
        Assert.Equal("Outside", (await db.MenuCategories.AsNoTracking().SingleAsync(x => x.Id == category.Id, Token)).Name);
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real management transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task WaiterCanChangeTableStatusButCannotAdministerRestaurant()
    {
        var actor = await BootstrapAsync();
        await using var scope = _provider!.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IRestaurantManagementService>();
        var table = await TableAsync(service, actor.Id);
        var employees = scope.ServiceProvider.GetRequiredService<IEmployeeService>();
        Assert.True((await employees.CreateAsync(actor.Id, new("waiter@example.test", "Waiter", Password, StaffRoles.Waiter), Token)).Succeeded);
        var waiter = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("waiter@example.test"))!;
        Assert.True((await service.SetTableStatusAsync(waiter.Id, new(table.Id, TableStatus.Reserved, table.Version), Token)).Succeeded);
        Assert.False((await service.GetSettingsAsync(waiter.Id, Token)).Allowed);
        Assert.True((await service.SaveCategoryAsync(waiter.Id, new(null, "Invalid", 0, false, null), Token)).Forbidden);
        Assert.True((await service.SaveTableAsync(waiter.Id, new(null, 2, 4, TableStatus.Available, false, null), Token)).Forbidden);
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real management transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task ActiveOrderPreventsReleasingOrArchivingItsTable()
    {
        var actor = await BootstrapAsync();
        await using var scope = _provider!.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IRestaurantManagementService>();
        var table = await TableAsync(service, actor.Id);
        Assert.True((await service.SetTableStatusAsync(actor.Id, new(table.Id, TableStatus.Occupied, table.Version), Token)).Succeeded);
        var db = scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        db.Add(new Order { RestaurantId = _restaurantId, RestaurantTableId = table.Id, OrderNumber = "TEST-1",
            IdempotencyKey = "active-order", RequestFingerprint = new string('a', 64), ReceiptTokenHash = new string('b', 64), CurrencyCode = "EUR" });
        await db.SaveChangesAsync(Token);
        table = (await service.GetTableAsync(actor.Id, table.Id, Token)).Value!;
        Assert.False((await service.SetTableStatusAsync(actor.Id, new(table.Id, TableStatus.Available, table.Version), Token)).Succeeded);
        Assert.False((await service.SaveTableAsync(actor.Id, new(table.Id, table.Number, table.Capacity, TableStatus.Available, true, table.Version), Token)).Succeeded);
        Assert.Equal(TableStatus.Occupied, (await service.GetTableAsync(actor.Id, table.Id, Token)).Value!.Status);
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real management transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task DuplicateTableNumbersReturnASanitizedValidationError()
    {
        var actor = await BootstrapAsync();
        await using var scope = _provider!.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IRestaurantManagementService>();
        await TableAsync(service, actor.Id);
        var result = await service.SaveTableAsync(actor.Id, new(null, 1, 4, TableStatus.Available, false, null), Token);
        Assert.False(result.Succeeded);
        Assert.Equal("That table number already exists in this restaurant.", Assert.Single(result.Errors));
        Assert.Single((await service.TablesAsync(actor.Id, 1, true, Token)).Value!.Items);
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real management transaction tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task ImageUploadChangesVersionAndStaleRemovalCannotClearIt()
    {
        var actor = await BootstrapAsync();
        await using var scope = _provider!.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IRestaurantManagementService>();
        var product = await ProductAsync(service, actor.Id, (await CategoryAsync(service, actor.Id)).Id);
        using var surface = SKSurface.Create(new SKImageInfo(8, 8));
        surface.Canvas.Clear(SKColors.ForestGreen);
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        Assert.True((await service.UploadImageAsync(actor.Id, product.Id, product.Version, stream, "image.png", "image/png", Token)).Succeeded);
        var current = (await service.GetMenuAsync(actor.Id, product.Id, Token)).Value!;
        Assert.True(current.HasImage);
        Assert.NotEqual(product.Version, current.Version);
        Assert.True((await service.RemoveImageAsync(actor.Id, product.Id, product.Version, Token)).Conflict);
        var key = (await service.ImageKeyAsync(actor.Id, product.Id, Token)).Value!;
        using var stored = scope.ServiceProvider.GetRequiredService<IProductImageStore>().OpenRead(key);
        Assert.NotNull(stored);
        Assert.True((await service.RemoveImageAsync(actor.Id, product.Id, current.Version, Token)).Succeeded);
        Assert.Null((await service.ImageKeyAsync(actor.Id, product.Id, Token)).Value);
    }

    private static async Task<CategoryDetails> CategoryAsync(IRestaurantManagementService service, Guid actor)
    {
        Assert.True((await service.SaveCategoryAsync(actor, new(null, "Lunch", 0, false, null), Token)).Succeeded);
        return Assert.Single((await service.CategoriesAsync(actor, 1, null, false, Token)).Value!.Items);
    }
    private static async Task<MenuDetails> ProductAsync(IRestaurantManagementService service, Guid actor, Guid category)
    {
        Assert.True((await service.SaveMenuAsync(actor, new(null, category, "Soup", "", 8.50m, true, false, null), Token)).Succeeded);
        return Assert.Single((await service.MenuAsync(actor, 1, null, false, Token)).Value!.Items);
    }
    private static async Task<TableDetails> TableAsync(IRestaurantManagementService service, Guid actor)
    {
        Assert.True((await service.SaveTableAsync(actor, new(null, 1, 4, TableStatus.Available, false, null), Token)).Succeeded);
        return Assert.Single((await service.TablesAsync(actor, 1, false, Token)).Value!.Items);
    }
    private static MenuInput MenuInputFor(MenuDetails x, bool archived = false) =>
        new(x.Id, x.CategoryId, x.Name, x.Description, x.Price, true, archived, x.Version);
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
