using Microsoft.Extensions.DependencyInjection.Extensions;
using ServeFlow.Application.Analytics;
using ServeFlow.Infrastructure.Analytics;
using ServeFlow.Application.Operations;
using ServeFlow.Infrastructure.Operations;
using ServeFlow.Application.Ordering;
using ServeFlow.Infrastructure.Ordering;
using ServeFlow.Application.Management;
using ServeFlow.Infrastructure.Management;
using Microsoft.AspNetCore.Identity;
using ServeFlow.Application.Accounts;
using ServeFlow.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ServeFlow.Infrastructure.Configuration;
using ServeFlow.Infrastructure.Persistence;

namespace ServeFlow.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.TryAddSingleton<IHostEnvironment>(environment);
        var connectionString = SqlServerConnection.Validate(
            configuration.GetConnectionString("ServeFlow"), environment.IsDevelopment());

        services.AddDbContext<ServeFlowDbContext>(options => options.UseSqlServer(connectionString, sql =>
        {
            sql.EnableRetryOnFailure(3);
            sql.CommandTimeout(30);
            sql.MigrationsAssembly(typeof(ServeFlowDbContext).Assembly.FullName);
        }));
        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
        {
            options.User.RequireUniqueEmail = true;
            options.Password.RequiredLength = 12;
            options.Password.RequiredUniqueChars = 4;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.AllowedForNewUsers = true;
        }).AddEntityFrameworkStores<ServeFlowDbContext>()
            .AddSignInManager<ActiveSignInManager>()
            .AddClaimsPrincipalFactory<StaffClaimsPrincipalFactory>()
            .AddDefaultTokenProviders();
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);
        services.AddScoped<ISecurityStampValidator, ActiveSecurityStampValidator>();
        services.AddScoped<IAccountActivity, AccountActivity>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<AdministratorProvisioner>();
        services.AddScoped<DevelopmentDemoProvisioner>();
        services.AddOptions<ProductImageOptions>().Bind(configuration.GetSection("Uploads"))
            .Validate(options => environment.IsDevelopment() || Path.IsPathFullyQualified(options.RootPath ?? ""),
                "Configure an absolute Uploads:RootPath in production.").ValidateOnStart();
        services.AddSingleton<IProductImageStore, ProductImageStore>();
        services.AddScoped<RestaurantManagementService>();
        services.AddScoped<IRestaurantManagementService>(sp => sp.GetRequiredService<RestaurantManagementService>());
        services.AddScoped<IMenuOptionService>(sp => sp.GetRequiredService<RestaurantManagementService>());
        services.TryAddScoped<IOrderChangeNotifier, NoOpOrderNotifier>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IAnalyticsService, AnalyticsService>();
        services.AddScoped<IOperationsService, OperationsService>();
        services.AddScoped<IOrderingService, OrderingService>();
        return services;
    }
}
