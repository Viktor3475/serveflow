using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using ServeFlow.Application.Security;
using ServeFlow.Infrastructure.Identity;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using ServeFlow.Application;
using ServeFlow.Infrastructure;
using ServeFlow.Web.Health;

var bootstrapLogger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

try
{
    var demo = args.Contains("--provision-demo", StringComparer.Ordinal);
    var administratorCommand = args.Contains("--provision-admin", StringComparer.Ordinal);
    if (demo && administratorCommand) throw new InvalidOperationException("Choose one provisioning command.");
    var builder = WebApplication.CreateBuilder(args.Where(argument => argument != "--provision-admin" && argument != "--provision-demo").ToArray());
    if (demo) _ = DevelopmentDemoSettings.Read(builder.Configuration, builder.Environment.IsDevelopment());
    var trustedProxies = ServeFlow.Web.Configuration.TrustedProxyConfiguration.Read(builder.Configuration);
    if (trustedProxies.Count > 0)
        builder.Services.Configure<ForwardedHeadersOptions>(options => ServeFlow.Web.Configuration.TrustedProxyConfiguration.Apply(options, trustedProxies));
    builder.Services.AddSerilog((services, configuration) => configuration
        .ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(), preserveStaticLogger: true);

    builder.Services.AddSingleton<ServeFlow.Web.Ordering.TableSession>();
    builder.Services.AddSingleton<ServeFlow.Web.Realtime.OrderConnections>();
    builder.Services.AddSignalR(options => { options.EnableDetailedErrors = false; options.MaximumReceiveMessageSize = 4096; options.MaximumParallelInvocationsPerClient = 1; options.AddFilter<ServeFlow.Web.Realtime.OrdersHubRateFilter>(); });
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
    builder.Services.AddScoped<ServeFlow.Application.Operations.IOrderChangeNotifier, ServeFlow.Web.Realtime.SignalROrderNotifier>();
    builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new AuthorizeFilter());
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    });
    builder.Services.ConfigureApplicationCookie(options =>
    {
        options.Cookie.Name = builder.Environment.IsDevelopment() ? "ServeFlow.Auth" : "__Host-ServeFlow.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });
    builder.Services.AddAntiforgery(options =>
    {
        options.HeaderName = "X-CSRF-TOKEN";
        options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    });
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy(StaffPolicies.Administration, policy => policy.RequireAuthenticatedUser()
            .RequireClaim(StaffClaims.RestaurantId).RequireRole(StaffRoles.Administrator));
        options.AddPolicy(StaffPolicies.DiningRoom, policy => policy.RequireAuthenticatedUser()
            .RequireClaim(StaffClaims.RestaurantId).RequireRole(StaffRoles.Administrator, StaffRoles.Waiter));
        options.AddPolicy(StaffPolicies.Kitchen, policy => policy.RequireAuthenticatedUser()
            .RequireClaim(StaffClaims.RestaurantId).RequireRole(StaffRoles.Administrator, StaffRoles.KitchenStaff));
        options.AddPolicy(StaffPolicies.Staff, policy => policy.RequireAuthenticatedUser()
            .RequireClaim(StaffClaims.RestaurantId).RequireRole(StaffRoles.EmployeeRoles));
    });
    builder.Services.AddRateLimiter(options =>
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        foreach (var (name, permits) in new[] { ("dining-exchange", 10), ("dining-read", 120), ("dining-order", 10) })
            options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        foreach (var (name, permits) in new[] { ("operations-read", 120), ("operations-write", 60), ("analytics-read", 20) })
            options.AddPolicy(name, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown", _ => new FixedWindowRateLimiterOptions
                { PermitLimit = permits, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        options.AddPolicy("image-upload", context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "unknown", _ => new FixedWindowRateLimiterOptions
            { PermitLimit = 5, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        options.AddPolicy("login", context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
            { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    });
    var protection = builder.Services.AddDataProtection().SetApplicationName("ServeFlow");
    var configuredKeyPath = builder.Configuration["DataProtection:KeyPath"];
    if (!string.IsNullOrWhiteSpace(configuredKeyPath)) protection.PersistKeysToFileSystem(new DirectoryInfo(configuredKeyPath));
    if (!builder.Environment.IsDevelopment())
    {
        var keyPath = configuredKeyPath
            ?? throw new InvalidOperationException("Configure DataProtection:KeyPath for persistent production cookies.");
        var certificatePath = builder.Configuration["DataProtection:CertificatePath"]
            ?? throw new InvalidOperationException("Configure DataProtection:CertificatePath to encrypt production keys.");
        var certificate = X509CertificateLoader.LoadPkcs12FromFile(certificatePath,
            builder.Configuration["DataProtection:CertificatePassword"], X509KeyStorageFlags.EphemeralKeySet);
        if (!certificate.HasPrivateKey) throw new InvalidOperationException("The Data Protection certificate needs a private key.");
        protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath)).ProtectKeysWithCertificate(certificate);
    }
    builder.Services.AddProblemDetails();
    builder.Services.AddHealthChecks()
        .AddCheck<SqlServerHealthCheck>("sql-server", tags: ["database"]);

    var app = builder.Build();
    if (administratorCommand || demo)
    {
        await using var scope = app.Services.CreateAsyncScope();
        if (demo) await scope.ServiceProvider.GetRequiredService<DevelopmentDemoProvisioner>().ProvisionAsync(CancellationToken.None);
        else await scope.ServiceProvider.GetRequiredService<AdministratorProvisioner>().ProvisionAsync(CancellationToken.None);
        bootstrapLogger.Information("Provisioning completed; no credentials were written to the log");
        await app.DisposeAsync();
        return;
    }
    if (trustedProxies.Count > 0) app.UseForwardedHeaders();
    app.UseSerilogRequestLogging();
    app.UseExceptionHandler();
    app.UseStatusCodePages();

    if (!app.Environment.IsDevelopment())
    {
        app.UseHsts();
        app.UseHttpsRedirection();
    }

    app.Use(async (context, next) =>
    {
        if (context.Request.Path.StartsWithSegments("/hubs/orders") && context.Request.Headers.TryGetValue("Origin", out var origin)
            && (!Uri.TryCreate(origin.ToString(), UriKind.Absolute, out var originUri) || originUri.Authority != context.Request.Host.Value || originUri.Scheme != context.Request.Scheme))
        { context.Response.StatusCode = 403; return; }
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
        context.Response.Headers["Content-Security-Policy"] =
            "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; " +
            "font-src 'self'; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
        if (context.Request.Path.StartsWithSegments("/Dining") || context.Request.Path.StartsWithSegments("/api/dining") || context.Request.Path.StartsWithSegments("/TableQr") || context.Request.Path.StartsWithSegments("/api/staff") || context.Request.Path.StartsWithSegments("/api/operations") || context.Request.Path.StartsWithSegments("/Operations") || context.Request.Path.StartsWithSegments("/Kitchen") || context.Request.Path.StartsWithSegments("/Dashboard") || context.Request.Path.StartsWithSegments("/hubs/orders"))
        {
            context.Response.Headers.CacheControl = "no-store";
            context.Response.Headers["Referrer-Policy"] = "no-referrer";
        }
        await next(context);
    });

    app.UseStaticFiles();
    app.UseRouting();
    app.UseAuthentication();
    app.UseAuthorization();
    app.UseRateLimiter();
    app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false }).AllowAnonymous();
    app.MapHealthChecks("/health/database", new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("database")
    }).AllowAnonymous();
    app.MapHub<ServeFlow.Web.Realtime.OrdersHub>("/hubs/orders", options => options.CloseOnAuthenticationExpiration = true).AllowAnonymous().RequireRateLimiting("dining-read");
    app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
    await app.RunAsync();
}
catch (Exception exception)
{
    bootstrapLogger.Fatal(exception, "ServeFlow terminated unexpectedly");
    throw;
}
finally
{
    await bootstrapLogger.DisposeAsync();
}

// Exposes the entry point to WebApplicationFactory integration tests.
public partial class Program;
