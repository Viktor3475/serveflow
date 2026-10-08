using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ServeFlow.Domain.Entities;
using ServeFlow.Domain.Enums;
using ServeFlow.Infrastructure.Persistence;

namespace ServeFlow.IntegrationTests;

// Each test gets its own generated database; never deletes or migrates the supplied database name.
public sealed class SqlServerDatabaseTests : IAsyncLifetime
{
    public static bool SqlServerConfigured => !string.IsNullOrWhiteSpace(
        Environment.GetEnvironmentVariable("SERVEFLOW_TEST_SQLSERVER"));

    private string? _connectionString;
    private bool _initializationStarted;

    public async ValueTask InitializeAsync()
    {
        if (!SqlServerConfigured) return;
        var builder = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SERVEFLOW_TEST_SQLSERVER"))
        {
            InitialCatalog = $"ServeFlowTests_{Guid.NewGuid():N}",
            Pooling = false
        };
        _connectionString = builder.ConnectionString;
        _initializationStarted = true;
        await using var db = CreateContext();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (!_initializationStarted) return;
        await using var db = CreateContext();
        // Teardown must run even when the test cancellation token has been cancelled.
        await db.Database.EnsureDeletedAsync(CancellationToken.None);
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER to run against real SQL Server.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task MigrationAppliesAndHasNoPendingMigrations()
    {
        await using var db = CreateContext();
        Assert.Equal(db.Database.GetMigrations().Count(), (await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken)).Count());
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
        Assert.True(await db.Database.CanConnectAsync(TestContext.Current.CancellationToken));
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER to run against real SQL Server.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task CrossRestaurantCategoryAssignmentIsRejected()
    {
        await using var db = CreateContext();
        var first = new Restaurant { Name = "First" };
        var second = new Restaurant { Name = "Second" };
        var category = new MenuCategory { RestaurantId = first.Id, Name = "Lunch" };
        db.AddRange(first, second, category);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.MenuItems.Add(new MenuItem { RestaurantId = second.Id, MenuCategoryId = category.Id, Name = "Invalid", Price = 5m });
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER to run against real SQL Server.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task ConcurrentMenuUpdatesRaiseConcurrencyException()
    {
        var data = await SeedAsync();
        await using var first = CreateContext();
        await using var second = CreateContext();
        var a = await first.MenuItems.SingleAsync(x => x.Id == data.Item.Id, TestContext.Current.CancellationToken);
        var b = await second.MenuItems.SingleAsync(x => x.Id == data.Item.Id, TestContext.Current.CancellationToken);
        a.Price = 18m;
        await first.SaveChangesAsync(TestContext.Current.CancellationToken);
        b.Price = 20m;
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER to run against real SQL Server.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task MenuUpdatesLeaveHistoricalNamesAndPricesIntact()
    {
        var data = await SeedAsync();
        await using (var db = CreateContext())
        {
            var order = CreateOrder(data);
            order.Items.Add(CreateOrderItem(data, order));
            db.Add(order);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var menu = await db.MenuItems.SingleAsync(x => x.Id == data.Item.Id, TestContext.Current.CancellationToken);
            menu.Price = 99m;
            menu.Name = "Renamed";
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        await using var read = CreateContext();
        var historical = await read.OrderItems.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(12.50m, historical.UnitPriceSnapshot);
        Assert.Equal("Soup", historical.NameSnapshot);
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER to run against real SQL Server.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task RepeatedIdempotencyKeyIsRejectedByUniqueIndex()
    {
        var data = await SeedAsync();
        await using var db = CreateContext();
        db.Add(CreateOrder(data, "same-request"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        db.Add(CreateOrder(data, "same-request"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Theory(Skip = "Set SERVEFLOW_TEST_SQLSERVER to run against real SQL Server.", SkipUnless = nameof(SqlServerConfigured))]
    [InlineData(0, 12.50)]
    [InlineData(1, -1)]
    public async Task InvalidQuantitiesAndPricesAreRejected(int quantity, double price)
    {
        var data = await SeedAsync();
        await using var db = CreateContext();
        var order = CreateOrder(data);
        order.Items.Add(new OrderItem
        {
            RestaurantId = data.Restaurant.Id, OrderId = order.Id, MenuItemId = data.Item.Id,
            NameSnapshot = "Soup", Quantity = quantity, UnitPriceSnapshot = (decimal)price
        });
        db.Add(order);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER to run against real SQL Server.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task OptionForAnotherMenuItemIsRejected()
    {
        var data = await SeedAsync();
        await using var db = CreateContext();
        var otherItem = new MenuItem { RestaurantId = data.Restaurant.Id, MenuCategoryId = data.Item.MenuCategoryId, Name = "Other" };
        var option = new MenuItemOption { RestaurantId = data.Restaurant.Id, MenuItemId = otherItem.Id, Name = "Extra" };
        db.AddRange(otherItem, option);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var order = CreateOrder(data);
        var item = CreateOrderItem(data, order);
        item.Options.Add(new OrderItemOption
        {
            RestaurantId = data.Restaurant.Id, OrderItemId = item.Id, MenuItemId = data.Item.Id,
            MenuItemOptionId = option.Id, NameSnapshot = "Extra", AdditionalPriceSnapshot = 1m
        });
        order.Items.Add(item);
        db.Add(order);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER to run against real SQL Server.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task SuccessfulPaymentRequiresPaidAtAndMatchingCurrency()
    {
        var data = await SeedAsync();
        var order = CreateOrder(data);
        await using (var db = CreateContext())
        {
            db.Add(order);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            db.Add(new Payment { RestaurantId = data.Restaurant.Id, OrderId = order.Id, Amount = 12.50m,
                CurrencyCode = "EUR", Status = PaymentStatus.Succeeded });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        }
        await using var other = CreateContext();
        other.Add(new Payment { RestaurantId = data.Restaurant.Id, OrderId = order.Id, Amount = 12.50m,
            CurrencyCode = "USD", Status = PaymentStatus.Pending });
        await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER to run against real SQL Server.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task PendingAndFailedPaymentsDoNotCountAsCollections()
    {
        var data = await SeedAsync();
        await using var db = CreateContext();
        var order = CreateOrder(data);
        order.Payments.Add(new Payment { RestaurantId = data.Restaurant.Id, OrderId = order.Id, Amount = 12.50m,
            CurrencyCode = "EUR", Status = PaymentStatus.Pending });
        order.Payments.Add(new Payment { RestaurantId = data.Restaurant.Id, OrderId = order.Id, Amount = 12.50m,
            CurrencyCode = "EUR", Status = PaymentStatus.Failed });
        order.Payments.Add(new Payment { RestaurantId = data.Restaurant.Id, OrderId = order.Id, Amount = 12.50m,
            CurrencyCode = "EUR", Status = PaymentStatus.Succeeded, PaidAt = DateTimeOffset.UtcNow });
        db.Add(order);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var collected = await db.Payments.Where(x => x.Status == PaymentStatus.Succeeded)
            .SumAsync(x => x.Amount, TestContext.Current.CancellationToken);
        Assert.Equal(12.50m, collected);
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER to run against real SQL Server.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task ReferencedMenuItemCannotBeDeleted()
    {
        var data = await SeedAsync();
        await using var db = CreateContext();
        var order = CreateOrder(data);
        order.Items.Add(CreateOrderItem(data, order));
        db.Add(order);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        // Do not let tracked dependents intercept deletion before SQL Server checks its foreign key.
        db.ChangeTracker.Clear();
        var menu = await db.MenuItems.SingleAsync(x => x.Id == data.Item.Id, TestContext.Current.CancellationToken);
        db.Remove(menu);
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private async Task<SeedData> SeedAsync()
    {
        await using var db = CreateContext();
        var restaurant = new Restaurant { Name = "Test restaurant" };
        var table = new RestaurantTable { RestaurantId = restaurant.Id, Number = 1, Capacity = 4 };
        var category = new MenuCategory { RestaurantId = restaurant.Id, Name = "Lunch" };
        var item = new MenuItem { RestaurantId = restaurant.Id, MenuCategoryId = category.Id, Name = "Soup", Price = 12.50m };
        db.AddRange(restaurant, table, category, item);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new SeedData(restaurant, table, item);
    }

    private static Order CreateOrder(SeedData data, string? key = null) => new()
    {
        RestaurantId = data.Restaurant.Id, RestaurantTableId = data.Table.Id,
        OrderNumber = Guid.NewGuid().ToString("N"), IdempotencyKey = key ?? Guid.NewGuid().ToString("N"),
        RequestFingerprint = new string('a', 64), ReceiptTokenHash = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
        CurrencyCode = "EUR", Total = 12.50m
    };

    private static OrderItem CreateOrderItem(SeedData data, Order order) => new()
    {
        RestaurantId = data.Restaurant.Id, OrderId = order.Id, MenuItemId = data.Item.Id,
        NameSnapshot = data.Item.Name, UnitPriceSnapshot = data.Item.Price, Quantity = 1
    };

    private ServeFlowDbContext CreateContext() => new(new DbContextOptionsBuilder<ServeFlowDbContext>()
        .UseSqlServer(_connectionString ?? throw new InvalidOperationException("SQL Server test connection is not configured.")).Options);

    private sealed record SeedData(Restaurant Restaurant, RestaurantTable Table, MenuItem Item);
}
