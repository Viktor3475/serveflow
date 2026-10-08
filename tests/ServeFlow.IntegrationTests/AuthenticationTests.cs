using ServeFlow.Application.Management;
using ServeFlow.Application.Ordering;
using ServeFlow.Application.Operations;
using ServeFlow.Application.Analytics;
using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ServeFlow.Application.Security;
using ServeFlow.Infrastructure.Identity;

namespace ServeFlow.IntegrationTests;

public sealed partial class AuthenticationTests
{
    internal const string TestPassword = "Test-only-Strong42!";
    internal const string TestEmail = "staff@example.test";

    [Fact]
    public async Task AnonymousEmployeePageChallengesToLogin()
    {
        await using var factory = new AuthFactory();
        using var client = Client(factory);
        using var response = await client.GetAsync("/Employees", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task LoginWithoutAntiforgeryTokenIsRejected()
    {
        await using var factory = new AuthFactory();
        using var client = Client(factory);
        using var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Email"] = TestEmail, ["Password"] = TestPassword }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(StaffRoles.Waiter)]
    [InlineData(StaffRoles.KitchenStaff)]
    [InlineData(StaffRoles.Customer)]
    public async Task NonAdministratorsCannotOpenEmployeeManagement(string role)
    {
        await using var factory = new AuthFactory();
        using var client = Client(factory);
        await SeedAsync(factory, role);
        using var login = await LoginAsync(client);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        using var response = await client.GetAsync("/Employees/Create", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task AdministratorCanOpenCreateEmployeeForm()
    {
        await using var factory = new AuthFactory();
        using var client = Client(factory);
        await SeedAsync(factory, StaffRoles.Administrator);
        using var login = await LoginAsync(client);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        using var response = await client.GetAsync("/Employees/Create", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Create account", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LoginDoesNotRedirectToAnExternalReturnUrl()
    {
        await using var factory = new AuthFactory();
        using var client = Client(factory);
        await SeedAsync(factory, StaffRoles.Waiter);
        using var response = await LoginAsync(client, "https://evil.example/steal");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location!.OriginalString);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), x => x.StartsWith("ServeFlow.Auth=") && x.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task FailedAttemptsLockTheAccountAndDoNotRevealItsExistence()
    {
        await using var factory = new AuthFactory();
        using var client = Client(factory);
        var user = await SeedAsync(factory, StaffRoles.Waiter);
        for (var i = 0; i < 5; i++)
        {
            using var failure = await LoginAsync(client, password: "Incorrect-test-password!");
            Assert.Equal(HttpStatusCode.OK, failure.StatusCode);
            Assert.Contains("Unable to sign in", await failure.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        }
        Assert.True(user.LockoutEnd > DateTimeOffset.UtcNow);
        using var correct = await LoginAsync(client);
        Assert.Equal(HttpStatusCode.OK, correct.StatusCode);
    }

    [Fact]
    public async Task ArchivedAccountCannotSignInAndItsExistingCookieIsRejected()
    {
        await using var factory = new AuthFactory();
        using var client = Client(factory);
        var user = await SeedAsync(factory, StaffRoles.Waiter);
        using var login = await LoginAsync(client);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        user.IsArchived = true;
        using var page = await client.GetAsync("/Account/ChangePassword", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Contains("/Account/Login", page.Headers.Location!.OriginalString);
        using var denied = await LoginAsync(client);
        Assert.Equal(HttpStatusCode.OK, denied.StatusCode);
    }

    [Fact]
    public async Task ArchivedRestaurantRevokesItsStaffCookie()
    {
        await using var factory = new AuthFactory();
        using var client = Client(factory);
        await SeedAsync(factory, StaffRoles.Waiter);
        using var login = await LoginAsync(client);
        factory.Activity.RestaurantActive = false;
        using var page = await client.GetAsync("/Account/ChangePassword", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
    }

    [Fact]
    public async Task SecurityStampRotationRevokesAnExistingCookie()
    {
        await using var factory = new AuthFactory();
        using var client = Client(factory);
        var user = await SeedAsync(factory, StaffRoles.Administrator);
        using var login = await LoginAsync(client);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            Assert.True((await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().UpdateSecurityStampAsync(user)).Succeeded);
        }
        using var page = await client.GetAsync("/Employees/Create", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Contains("/Account/Login", page.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task LogoutRequiresPostAndAntiforgeryAndEndsTheSession()
    {
        await using var factory = new AuthFactory();
        using var client = Client(factory);
        await SeedAsync(factory, StaffRoles.Waiter);
        using var login = await LoginAsync(client);
        using var getLogout = await client.GetAsync("/Account/Logout", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, getLogout.StatusCode);
        using var withoutToken = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent([]), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);
        var token = await GetTokenAsync(client, "/Account/ChangePassword");
        using var logout = await client.PostAsync("/Account/Logout", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["__RequestVerificationToken"] = token }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        using var page = await client.GetAsync("/Account/ChangePassword", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
    }

    [Fact]
    public async Task PasswordChangeUsesIdentityAndKeepsTheCurrentSession()
    {
        await using var factory = new AuthFactory();
        using var client = Client(factory);
        var user = await SeedAsync(factory, StaffRoles.Waiter);
        using var login = await LoginAsync(client);
        var token = await GetTokenAsync(client, "/Account/ChangePassword");
        const string newPassword = "New-test-password42!";
        using var response = await client.PostAsync("/Account/ChangePassword", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["CurrentPassword"] = TestPassword, ["NewPassword"] = newPassword, ["ConfirmPassword"] = newPassword,
            ["__RequestVerificationToken"] = token
        }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await users.CheckPasswordAsync(user, newPassword));
        Assert.False(await users.CheckPasswordAsync(user, TestPassword));
        Assert.DoesNotContain(newPassword, user.PasswordHash!);
        using var page = await client.GetAsync("/Account/ChangePassword", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
    }

    [Fact]
    public async Task LoginThrottleReturnsTooManyRequests()
    {
        await using var factory = new AuthFactory();
        using var client = Client(factory);
        for (var i = 0; i < 10; i++) { using var response = await LoginAsync(client); Assert.Equal(HttpStatusCode.OK, response.StatusCode); }
        using var limited = await LoginAsync(client);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }

    [Theory]
    [InlineData(StaffRoles.Administrator, StaffPolicies.Administration, true)]
    [InlineData(StaffRoles.Waiter, StaffPolicies.DiningRoom, true)]
    [InlineData(StaffRoles.Waiter, StaffPolicies.Kitchen, false)]
    [InlineData(StaffRoles.KitchenStaff, StaffPolicies.Kitchen, true)]
    [InlineData(StaffRoles.KitchenStaff, StaffPolicies.DiningRoom, false)]
    [InlineData(StaffRoles.Customer, StaffPolicies.Staff, false)]
    public async Task StaffPoliciesEnforceTheirRoleBoundaries(string role, string policy, bool allowed)
    {
        await using var factory = new AuthFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
            new Claim(ClaimTypes.Role, role), new Claim(StaffClaims.RestaurantId, Guid.NewGuid().ToString())], "policy-test"));
        var result = await scope.ServiceProvider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, null, policy);
        Assert.Equal(allowed, result.Succeeded);
    }

    [Fact]
    public async Task StaffPoliciesRequireRestaurantMembership()
    {
        await using var factory = new AuthFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Role, StaffRoles.Administrator)], "policy-test"));
        var result = await scope.ServiceProvider.GetRequiredService<IAuthorizationService>()
            .AuthorizeAsync(principal, null, StaffPolicies.Administration);
        Assert.False(result.Succeeded);
    }

    internal static HttpClient Client(AuthFactory factory) => factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    internal static async Task<ApplicationUser> SeedAsync(AuthFactory factory, string role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        Assert.True((await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.NewGuid() })).Succeeded);
        var user = new ApplicationUser { Id = Guid.NewGuid(), RestaurantId = Guid.NewGuid(), UserName = TestEmail,
            Email = TestEmail, DisplayName = "Test staff" };
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True((await users.CreateAsync(user, TestPassword)).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        return user;
    }

    internal static async Task<HttpResponseMessage> LoginAsync(HttpClient client, string? returnUrl = null, string password = TestPassword)
    {
        var token = await GetTokenAsync(client, "/Account/Login");
        return await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Email"] = TestEmail, ["Password"] = password, ["ReturnUrl"] = returnUrl ?? "", ["__RequestVerificationToken"] = token }), TestContext.Current.CancellationToken);
    }

    internal static async Task<string> GetTokenAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var match = AntiforgeryPattern().Match(html);
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryPattern();

    internal sealed class AuthFactory : WebApplicationFactory<Program>
    {
        public IRestaurantManagementService? Management { get; init; }
        public IOrderingService? Ordering { get; init; }
        public IOperationsService? Operations { get; init; }
        public IAnalyticsService? Analytics { get; init; }
        public TestAccountActivity Activity { get; } = new();
        private readonly IdentityTestUserStore _users = new();
        private readonly IdentityTestRoleStore _roles = new();
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:ServeFlow", "Server=127.0.0.1,1;Database=AuthTests;Integrated Security=true;Encrypt=true;TrustServerCertificate=true");
            builder.ConfigureServices(services =>
            {
                services.AddDataProtection().UseEphemeralDataProtectionProvider();
                services.RemoveAll<IUserStore<ApplicationUser>>();
                services.RemoveAll<IRoleStore<IdentityRole<Guid>>>();
                services.RemoveAll<IAccountActivity>();
                services.AddSingleton<IUserStore<ApplicationUser>>(_users);
                services.AddSingleton<IRoleStore<IdentityRole<Guid>>>(_roles);
                services.AddSingleton<IAccountActivity>(Activity);
                if (Analytics is not null)
                {
                    services.RemoveAll<IAnalyticsService>();
                    services.AddSingleton(Analytics);
                }
                if (Operations is not null)
                {
                    services.RemoveAll<IOperationsService>();
                    services.AddSingleton(Operations);
                }
                if (Ordering is not null)
                {
                    services.RemoveAll<IOrderingService>();
                    services.AddSingleton(Ordering);
                }
                if (Management is not null)
                {
                    services.RemoveAll<IRestaurantManagementService>();
                    services.AddSingleton(Management);
                }
            });
        }
    }
}
