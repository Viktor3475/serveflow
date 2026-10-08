using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using ServeFlow.Domain.Entities;
using ServeFlow.Infrastructure.Identity;
using ServeFlow.Infrastructure.Persistence;

namespace ServeFlow.UnitTests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void ModelContainsDomainAndIdentityTablesWithoutShadowDomainProperties()
    {
        using var db = CreateContext();
        var model = db.Model;
        Assert.NotNull(model.FindEntityType(typeof(ApplicationUser)));
        var domainTypes = model.GetEntityTypes().Where(e => e.ClrType.Namespace == "ServeFlow.Domain.Entities").ToArray();
        Assert.Equal(10, domainTypes.Length);
        Assert.All(domainTypes, e => Assert.DoesNotContain(e.GetProperties(), p => p.IsShadowProperty()));
    }

    [Fact]
    public void AllDomainMoneyUsesExactDecimalPrecision()
    {
        using var db = CreateContext();
        var money = db.Model.GetEntityTypes().SelectMany(e => e.GetProperties())
            .Where(p => p.ClrType == typeof(decimal)).ToArray();
        Assert.Equal(6, money.Length);
        Assert.All(money, p =>
        {
            Assert.Equal(18, p.GetPrecision());
            Assert.Equal(2, p.GetScale());
        });
    }

    [Theory]
    [InlineData(typeof(Restaurant))]
    [InlineData(typeof(RestaurantTable))]
    [InlineData(typeof(MenuCategory))]
    [InlineData(typeof(MenuItem))]
    [InlineData(typeof(MenuItemOption))]
    [InlineData(typeof(Order))]
    [InlineData(typeof(Payment))]
    public void MutableAggregatesUseDatabaseGeneratedConcurrencyVersions(Type entityType)
    {
        using var db = CreateContext();
        var version = db.Model.FindEntityType(entityType)!.FindProperty("Version")!;
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
        Assert.Equal("rowversion", version.GetColumnType());
    }

    [Fact]
    public void RelationshipsEnforceRestaurantAndOptionOwnership()
    {
        using var db = CreateContext();
        var item = db.Model.FindEntityType(typeof(MenuItem))!;
        Assert.Contains(item.GetForeignKeys(), fk => fk.Properties.Select(p => p.Name)
            .SequenceEqual(["RestaurantId", "MenuCategoryId"]));
        var option = db.Model.FindEntityType(typeof(OrderItemOption))!;
        Assert.Equal(2, option.GetForeignKeys().Count());
        Assert.All(option.GetForeignKeys(), fk => Assert.Equal(3, fk.Properties.Count));
        Assert.All(option.GetForeignKeys(), fk => Assert.Contains(fk.Properties, p => p.Name == "MenuItemId"));
        var payment = db.Model.FindEntityType(typeof(Payment))!;
        Assert.Contains(payment.GetForeignKeys().Single().Properties, p => p.Name == "CurrencyCode");
    }

    [Fact]
    public void HistoricalReferencesDoNotCascadeDelete()
    {
        using var db = CreateContext();
        var domainForeignKeys = db.Model.GetEntityTypes()
            .Where(e => e.ClrType.Namespace == "ServeFlow.Domain.Entities")
            .SelectMany(e => e.GetForeignKeys());
        Assert.All(domainForeignKeys, fk => Assert.Equal(DeleteBehavior.NoAction, fk.DeleteBehavior));
    }

    [Fact]
    public void PriceAndNameSnapshotsCannotBeUpdatedAfterInsertion()
    {
        using var db = CreateContext();
        var item = db.Model.FindEntityType(typeof(OrderItem))!;
        Assert.Equal(PropertySaveBehavior.Throw, item.FindProperty(nameof(OrderItem.NameSnapshot))!.GetAfterSaveBehavior());
        Assert.Equal(PropertySaveBehavior.Throw, item.FindProperty(nameof(OrderItem.UnitPriceSnapshot))!.GetAfterSaveBehavior());
        var option = db.Model.FindEntityType(typeof(OrderItemOption))!;
        Assert.Equal(PropertySaveBehavior.Throw, option.FindProperty(nameof(OrderItemOption.AdditionalPriceSnapshot))!.GetAfterSaveBehavior());
    }

    [Fact]
    public void DatabaseConstraintsAndFilteredIdempotencyIndexesArePresent()
    {
        using var db = CreateContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var order = model.FindEntityType(typeof(Order))!;
        Assert.Contains(order.GetIndexes(), i => i.IsUnique && i.Properties.Select(p => p.Name)
            .SequenceEqual(["RestaurantId", "RestaurantTableId", "IdempotencyKey"]));
        var table = model.FindEntityType(typeof(RestaurantTable))!;
        Assert.Contains(table.GetIndexes(), i => i.IsUnique && i.GetFilter() == "[QrTokenHash] IS NOT NULL");
        Assert.Contains(model.FindEntityType(typeof(OrderItem))!.GetCheckConstraints(), c => c.Name == "CK_OrderItems_Quantity");
        Assert.Contains(model.FindEntityType(typeof(Payment))!.GetCheckConstraints(), c => c.Name == "CK_Payments_PaidAt");
    }

    [Fact]
    public void MigrationSnapshotMatchesTheCurrentModelAndProducesSqlServerScript()
    {
        using var db = CreateContext();
        Assert.False(db.Database.HasPendingModelChanges());
        Assert.Contains(db.Database.GetMigrations(), x => x.EndsWith("_InitialSchema", StringComparison.Ordinal));
        var script = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("CREATE TABLE [Restaurants]", script);
        Assert.Contains("rowversion", script);
        Assert.Contains("CK_OrderItems_Quantity", script);
        Assert.Contains("__EFMigrationsHistory", script);
    }

    private static ServeFlowDbContext CreateContext() => new(new DbContextOptionsBuilder<ServeFlowDbContext>()
        .UseSqlServer("Server=localhost;Database=ServeFlowModelTests;Integrated Security=true;Encrypt=true")
        .Options);
}
