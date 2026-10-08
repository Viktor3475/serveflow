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
using ServeFlow.Application.Security;
using ServeFlow.Domain.Entities;
using ServeFlow.Infrastructure;
using ServeFlow.Infrastructure.Identity;
using ServeFlow.Infrastructure.Persistence;

namespace ServeFlow.IntegrationTests;

public sealed class IdentitySqlServerTests : IAsyncLifetime
{
    public static bool SqlServerConfigured => SqlServerDatabaseTests.SqlServerConfigured;
    private const string Password = "Test-only-Strong42!";
    private readonly Guid _restaurantId = Guid.NewGuid();
    private ServiceProvider? _provider;
    private string? _connection;

    public async ValueTask InitializeAsync()
    {
        if (!SqlServerConfigured) return;
        _connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SERVEFLOW_TEST_SQLSERVER"))
            { InitialCatalog = $"ServeFlowTests_{Guid.NewGuid():N}", Pooling = false }.ConnectionString;
        await using (var db = Context()) { await db.Database.MigrateAsync(TestContext.Current.CancellationToken); }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:ServeFlow"] = _connection,
            ["Bootstrap:RestaurantId"] = _restaurantId.ToString(), ["Bootstrap:RestaurantName"] = "Identity test restaurant",
            ["Bootstrap:Email"] = "root@example.test", ["Bootstrap:DisplayName"] = "Administrator", ["Bootstrap:Password"] = Password,
            ["Demo:WaiterEmail"] = "waiter@example.test", ["Demo:WaiterPassword"] = Password,
            ["Demo:KitchenEmail"] = "kitchen@example.test", ["Demo:KitchenPassword"] = Password
        }).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddSingleton<IHostEnvironment>(new TestEnvironment());
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
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real Identity database tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task DevelopmentDemoIsIdempotentAndKeepsPasswordsRolesAndMenu()
    {
        await using var scope = _provider!.CreateAsyncScope();
        var demo = scope.ServiceProvider.GetRequiredService<DevelopmentDemoProvisioner>();
        await demo.ProvisionAsync(TestContext.Current.CancellationToken);
        var db = scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        var hashes = await db.Users.OrderBy(x => x.Email).Select(x => x.PasswordHash).ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(3, hashes.Length); Assert.All(hashes, hash => Assert.NotEqual(Password, hash));
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await users.IsInRoleAsync((await users.FindByEmailAsync("waiter@example.test"))!, StaffRoles.Waiter));
        Assert.True(await users.IsInRoleAsync((await users.FindByEmailAsync("kitchen@example.test"))!, StaffRoles.KitchenStaff));
        await demo.ProvisionAsync(TestContext.Current.CancellationToken);
        Assert.Equal(hashes, await db.Users.OrderBy(x => x.Email).Select(x => x.PasswordHash).ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(3, await db.MenuItems.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(6, await db.RestaurantTables.CountAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await db.Orders.ToListAsync(TestContext.Current.CancellationToken));
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real Identity database tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task ProvisioningIsIdempotentAndNeverReplacesAnExistingPassword()
    {
        await using var scope = _provider!.CreateAsyncScope();
        var provisioning = scope.ServiceProvider.GetRequiredService<AdministratorProvisioner>();
        await provisioning.ProvisionAsync(TestContext.Current.CancellationToken);
        var db = scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        var firstHash = (await db.Users.SingleAsync(TestContext.Current.CancellationToken)).PasswordHash;
        await provisioning.ProvisionAsync(TestContext.Current.CancellationToken);
        Assert.Single(await db.Users.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Single(await db.Restaurants.ToListAsync(TestContext.Current.CancellationToken));
        Assert.Equal(4, await db.Roles.CountAsync(TestContext.Current.CancellationToken));
        var user = await db.Users.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(firstHash, user.PasswordHash);
        Assert.DoesNotContain(Password, user.PasswordHash!);
        Assert.True(await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().IsInRoleAsync(user, StaffRoles.Administrator));
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real Identity database tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task AdministratorCreatesEmployeeInTheirRestaurantWithHashedPassword()
    {
        var actor = await BootstrapAsync();
        await using var scope = _provider!.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IEmployeeService>();
        Assert.True((await service.CreateAsync(actor.Id,
            new CreateEmployeeRequest("waiter@example.test", "Waiter", Password, StaffRoles.Waiter), TestContext.Current.CancellationToken)).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var employee = (await users.FindByEmailAsync("waiter@example.test"))!;
        Assert.Equal(_restaurantId, employee.RestaurantId);
        Assert.True(await users.IsInRoleAsync(employee, StaffRoles.Waiter));
        Assert.True(await users.CheckPasswordAsync(employee, Password));
        Assert.DoesNotContain(Password, employee.PasswordHash!);
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real Identity database tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task EmployeeUpdatesRejectCrossRestaurantTargetsAndNonAdministratorActors()
    {
        var actor = await BootstrapAsync();
        await using var scope = _provider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var other = new Restaurant { Name = "Other" };
        db.Add(other);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var target = new ApplicationUser { Id = Guid.NewGuid(), RestaurantId = other.Id, DisplayName = "Outside",
            Email = "outside@example.test", UserName = "outside@example.test" };
        Assert.True((await users.CreateAsync(target, Password)).Succeeded);
        Assert.True((await users.AddToRoleAsync(target, StaffRoles.Waiter)).Succeeded);
        var service = scope.ServiceProvider.GetRequiredService<IEmployeeService>();
        var update = await service.UpdateAsync(actor.Id, new EditEmployeeRequest(target.Id, "Invalid", StaffRoles.Administrator,
            false, target.ConcurrencyStamp!), TestContext.Current.CancellationToken);
        Assert.True(update.Forbidden);
        var create = await service.CreateAsync(target.Id, new CreateEmployeeRequest("invalid@example.test", "Invalid", Password,
            StaffRoles.Waiter), TestContext.Current.CancellationToken);
        Assert.True(create.Forbidden);
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real Identity database tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task ArchiveAndRoleChangesRotateSecurityStampAndRejectStaleEdits()
    {
        var actor = await BootstrapAsync();
        await using var scope = _provider!.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IEmployeeService>();
        Assert.True((await service.CreateAsync(actor.Id, new CreateEmployeeRequest("worker@example.test", "Worker", Password,
            StaffRoles.Waiter), TestContext.Current.CancellationToken)).Succeeded);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var employee = (await users.FindByEmailAsync("worker@example.test"))!;
        var oldStamp = employee.SecurityStamp;
        var request = new EditEmployeeRequest(employee.Id, "Updated", StaffRoles.KitchenStaff, true, employee.ConcurrencyStamp!);
        Assert.True((await service.UpdateAsync(actor.Id, request, TestContext.Current.CancellationToken)).Succeeded);
        var updated = (await users.FindByIdAsync(employee.Id.ToString()))!;
        Assert.True(updated.IsArchived);
        Assert.NotEqual(oldStamp, updated.SecurityStamp);
        Assert.True(await users.IsInRoleAsync(updated, StaffRoles.KitchenStaff));
        Assert.False(await scope.ServiceProvider.GetRequiredService<IAccountActivity>().IsActiveAsync(updated));
        Assert.True((await service.UpdateAsync(actor.Id, request, TestContext.Current.CancellationToken)).Conflict);
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real Identity database tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task AdministratorCannotArchiveOrDemoteTheirOwnAccount()
    {
        var actor = await BootstrapAsync();
        await using var scope = _provider!.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IEmployeeService>();
        var result = await service.UpdateAsync(actor.Id, new EditEmployeeRequest(actor.Id, actor.DisplayName, StaffRoles.Waiter,
            true, actor.ConcurrencyStamp!), TestContext.Current.CancellationToken);
        Assert.False(result.Succeeded);
        var directory = await service.ListAsync(actor.Id, TestContext.Current.CancellationToken);
        Assert.True(directory.Allowed);
        Assert.Equal(StaffRoles.Administrator, Assert.Single(directory.Employees).Role);
    }

    [Fact(Skip = "Set SERVEFLOW_TEST_SQLSERVER for real Identity database tests.", SkipUnless = nameof(SqlServerConfigured))]
    public async Task SimultaneousAdministratorChangesLeaveAnActiveAdministrator()
    {
        var first = await BootstrapAsync();
        await using (var setup = _provider!.CreateAsyncScope())
        {
            var create = await setup.ServiceProvider.GetRequiredService<IEmployeeService>().CreateAsync(first.Id,
                new CreateEmployeeRequest("second@example.test", "Second admin", Password, StaffRoles.Administrator), TestContext.Current.CancellationToken);
            Assert.True(create.Succeeded);
        }
        await using var one = _provider!.CreateAsyncScope();
        await using var two = _provider!.CreateAsyncScope();
        var second = (await one.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByEmailAsync("second@example.test"))!;
        var operations = await Task.WhenAll(
            one.ServiceProvider.GetRequiredService<IEmployeeService>().UpdateAsync(first.Id,
                new EditEmployeeRequest(second.Id, second.DisplayName, StaffRoles.Waiter, true, second.ConcurrencyStamp!), TestContext.Current.CancellationToken),
            two.ServiceProvider.GetRequiredService<IEmployeeService>().UpdateAsync(second.Id,
                new EditEmployeeRequest(first.Id, first.DisplayName, StaffRoles.Waiter, true, first.ConcurrencyStamp!), TestContext.Current.CancellationToken));
        Assert.Single(operations, x => x.Succeeded);
        await using var read = _provider!.CreateAsyncScope();
        var db = read.ServiceProvider.GetRequiredService<ServeFlowDbContext>();
        var active = await (from user in db.Users join link in db.UserRoles on user.Id equals link.UserId
            join role in db.Roles on link.RoleId equals role.Id
            where !user.IsArchived && role.Name == StaffRoles.Administrator select user.Id)
            .CountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, active);
    }

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
