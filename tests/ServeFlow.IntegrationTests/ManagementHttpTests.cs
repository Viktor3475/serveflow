using System.Net;
using ServeFlow.Application.Accounts;
using ServeFlow.Application.Management;
using ServeFlow.Application.Security;
using ServeFlow.Domain.Enums;

namespace ServeFlow.IntegrationTests;

public sealed class ManagementHttpTests
{
    [Theory]
    [InlineData("/Restaurant")]
    [InlineData("/Categories")]
    [InlineData("/Menu")]
    [InlineData("/Tables")]
    public async Task ManagementPagesRequireSignIn(string path)
    {
        await using var factory = new AuthenticationTests.AuthFactory();
        using var client = AuthenticationTests.Client(factory);
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.OriginalString);
    }

    [Theory]
    [InlineData("/Restaurant")]
    [InlineData("/Categories")]
    [InlineData("/Categories/Create")]
    [InlineData("/Menu")]
    [InlineData("/Menu/Create")]
    [InlineData("/Tables")]
    [InlineData("/Tables/Create")]
    public async Task AdministratorPagesRenderWithAntiforgeryForms(string path)
    {
        await using var factory = new AuthenticationTests.AuthFactory { Management = new TestManagement() };
        using var client = AuthenticationTests.Client(factory);
        await AuthenticationTests.SeedAsync(factory, StaffRoles.Administrator);
        using var login = await AuthenticationTests.LoginAsync(client);
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("__RequestVerificationToken", html);
    }

    [Theory]
    [InlineData("/Restaurant", StaffRoles.Waiter)]
    [InlineData("/Categories/Create", StaffRoles.Waiter)]
    [InlineData("/Menu/Create", StaffRoles.Waiter)]
    [InlineData("/Tables/Create", StaffRoles.Waiter)]
    [InlineData("/Tables", StaffRoles.KitchenStaff)]
    public async Task RestrictedRolesAreDeniedBeforeCallingTheService(string path, string role)
    {
        await using var factory = new AuthenticationTests.AuthFactory();
        using var client = AuthenticationTests.Client(factory);
        await AuthenticationTests.SeedAsync(factory, role);
        using var login = await AuthenticationTests.LoginAsync(client);
        using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task WaiterCanViewTablesAndChangeOnlyTheirStatus()
    {
        var backend = new TestManagement();
        await using var factory = new AuthenticationTests.AuthFactory { Management = backend };
        using var client = AuthenticationTests.Client(factory);
        var actor = await AuthenticationTests.SeedAsync(factory, StaffRoles.Waiter);
        using var login = await AuthenticationTests.LoginAsync(client);
        var token = await AuthenticationTests.GetTokenAsync(client, $"/Tables/Status/{backend.TableId}");
        using var response = await client.PostAsync($"/Tables/Status/{backend.TableId}", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Id"] = backend.TableId.ToString(), ["Status"] = "Occupied", ["Version"] = TestManagement.Version,
            ["__RequestVerificationToken"] = token, ["ActorId"] = Guid.NewGuid().ToString(), ["RestaurantId"] = Guid.NewGuid().ToString()
        }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(actor.Id, backend.LastActorId);
        Assert.Equal(TableStatus.Occupied, backend.LastStatus!.Status);
    }

    [Fact]
    public async Task CategoryPostRejectsMissingAntiforgeryToken()
    {
        await using var factory = new AuthenticationTests.AuthFactory();
        using var client = AuthenticationTests.Client(factory);
        await AuthenticationTests.SeedAsync(factory, StaffRoles.Administrator);
        using var login = await AuthenticationTests.LoginAsync(client);
        using var response = await client.PostAsync("/Categories/Create", new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Name"] = "Lunch", ["SortOrder"] = "1" }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task StaleCategoryEditsReturnConflictWithAnAccessibleError()
    {
        var backend = new TestManagement { Result = OperationResult.Stale() };
        await using var factory = new AuthenticationTests.AuthFactory { Management = backend };
        using var client = AuthenticationTests.Client(factory);
        await AuthenticationTests.SeedAsync(factory, StaffRoles.Administrator);
        using var login = await AuthenticationTests.LoginAsync(client);
        var token = await AuthenticationTests.GetTokenAsync(client, $"/Categories/Edit/{backend.CategoryId}");
        using var response = await client.PostAsync($"/Categories/Edit/{backend.CategoryId}", new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Id"] = backend.CategoryId.ToString(), ["Name"] = "Changed", ["SortOrder"] = "1", ["Version"] = TestManagement.Version,
            ["__RequestVerificationToken"] = token }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("role=\"alert\"", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("Categories", "Edit")]
    [InlineData("Menu", "Edit")]
    [InlineData("Tables", "Edit")]
    [InlineData("Tables", "Status")]
    public async Task RouteAndPostedIdentityMustMatch(string controller, string action)
    {
        var backend = new TestManagement();
        await using var factory = new AuthenticationTests.AuthFactory { Management = backend };
        using var client = AuthenticationTests.Client(factory);
        await AuthenticationTests.SeedAsync(factory, StaffRoles.Administrator);
        using var login = await AuthenticationTests.LoginAsync(client);
        var id = controller == "Categories" ? backend.CategoryId : controller == "Menu" ? backend.MenuId : backend.TableId;
        var path = $"/{controller}/{action}/{id}";
        var token = await AuthenticationTests.GetTokenAsync(client, path);
        using var response = await client.PostAsync(path, new FormUrlEncodedContent(new Dictionary<string, string>
        { ["Id"] = Guid.NewGuid().ToString(), ["Name"] = "Invalid", ["SortOrder"] = "1", ["Version"] = TestManagement.Version,
            ["__RequestVerificationToken"] = token }), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task EditProductRendersSeparateImageFormsWithoutAcceptingStoragePaths()
    {
        var backend = new TestManagement();
        await using var factory = new AuthenticationTests.AuthFactory { Management = backend };
        using var client = AuthenticationTests.Client(factory);
        await AuthenticationTests.SeedAsync(factory, StaffRoles.Administrator);
        using var login = await AuthenticationTests.LoginAsync(client);
        using var response = await client.GetAsync($"/Menu/Edit/{backend.MenuId}", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("multipart/form-data", html);
        Assert.Contains("/Menu/UploadImage/", html);
        Assert.DoesNotContain("name=\"ImageStorageKey\"", html);
    }

    // HTTP tests use a fixed backend to verify the real cookie/authorization/Razor/CSRF pipeline.
    // Real ownership, archive and concurrency behavior is covered by opt-in SQL Server service tests.
    private sealed class TestManagement : IRestaurantManagementService
    {
        public static string Version => Convert.ToBase64String(new byte[8]);
        public Guid CategoryId { get; } = Guid.NewGuid();
        public Guid MenuId { get; } = Guid.NewGuid();
        public Guid TableId { get; } = Guid.NewGuid();
        public OperationResult Result { get; init; } = OperationResult.Success();
        public Guid LastActorId { get; private set; }
        public TableStatusInput? LastStatus { get; private set; }
        public Task<ManagementRead<RestaurantSettings>> GetSettingsAsync(Guid actorId, CancellationToken token) => Task.FromResult(new ManagementRead<RestaurantSettings>(true, new("Test restaurant", "EUR", "Europe/Sofia", Version)));
        public Task<OperationResult> SaveSettingsAsync(Guid actorId, SettingsInput input, CancellationToken token) => Task.FromResult(Result);
        public Task<ManagementRead<ManagementPage<CategoryDetails>>> CategoriesAsync(Guid actorId, int page, string? search, bool includeArchived, CancellationToken token) => Task.FromResult(new ManagementRead<ManagementPage<CategoryDetails>>(true, new([new(CategoryId, "Lunch", 1, false, Version)], 1, 1, 20, search ?? "", includeArchived, "EUR")));
        public Task<ManagementRead<CategoryDetails>> GetCategoryAsync(Guid actorId, Guid id, CancellationToken token) => Task.FromResult(new ManagementRead<CategoryDetails>(true, id == CategoryId ? new(CategoryId, "Lunch", 1, false, Version) : null));
        public Task<OperationResult> SaveCategoryAsync(Guid actorId, CategoryInput input, CancellationToken token) => Task.FromResult(Result);
        public Task<ManagementRead<IReadOnlyList<CategoryChoice>>> CategoryChoicesAsync(Guid actorId, CancellationToken token) => Task.FromResult(new ManagementRead<IReadOnlyList<CategoryChoice>>(true, [new(CategoryId, "Lunch", false)]));
        public Task<ManagementRead<ManagementPage<MenuDetails>>> MenuAsync(Guid actorId, int page, string? search, bool includeArchived, CancellationToken token) => Task.FromResult(new ManagementRead<ManagementPage<MenuDetails>>(true, new([new(MenuId, CategoryId, "Lunch", "Soup", "", 12.50m, true, false, false, Version)], 1, 1, 20, search ?? "", includeArchived, "EUR")));
        public Task<ManagementRead<MenuDetails>> GetMenuAsync(Guid actorId, Guid id, CancellationToken token) => Task.FromResult(new ManagementRead<MenuDetails>(true, id == MenuId ? new(MenuId, CategoryId, "Lunch", "Soup", "", 12.50m, true, false, false, Version) : null));
        public Task<OperationResult> SaveMenuAsync(Guid actorId, MenuInput input, CancellationToken token) => Task.FromResult(Result);
        public Task<ManagementRead<ManagementPage<TableDetails>>> TablesAsync(Guid actorId, int page, bool includeArchived, CancellationToken token) => Task.FromResult(new ManagementRead<ManagementPage<TableDetails>>(true, new([new(TableId, 1, 4, TableStatus.Available, false, Version)], 1, 1, 20, "", includeArchived, "EUR")));
        public Task<ManagementRead<TableDetails>> GetTableAsync(Guid actorId, Guid id, CancellationToken token) => Task.FromResult(new ManagementRead<TableDetails>(true, id == TableId ? new(TableId, 1, 4, TableStatus.Available, false, Version) : null));
        public Task<OperationResult> SaveTableAsync(Guid actorId, TableInput input, CancellationToken token) => Task.FromResult(Result);
        public Task<OperationResult> SetTableStatusAsync(Guid actorId, TableStatusInput input, CancellationToken token) { LastActorId = actorId; LastStatus = input; return Task.FromResult(Result); }
        public Task<OperationResult> UploadImageAsync(Guid actorId, Guid itemId, string version, Stream content, string fileName, string contentType, CancellationToken token) => Task.FromResult(Result);
        public Task<OperationResult> RemoveImageAsync(Guid actorId, Guid itemId, string version, CancellationToken token) => Task.FromResult(Result);
        public Task<ManagementRead<string>> ImageKeyAsync(Guid actorId, Guid itemId, CancellationToken token) => Task.FromResult(new ManagementRead<string>(true, null));
    }
}
