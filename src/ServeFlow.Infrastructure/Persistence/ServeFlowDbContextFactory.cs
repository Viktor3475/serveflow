using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using ServeFlow.Infrastructure.Configuration;

namespace ServeFlow.Infrastructure.Persistence;

public sealed class ServeFlowDbContextFactory : IDesignTimeDbContextFactory<ServeFlowDbContext>
{
    public ServeFlowDbContext CreateDbContext(string[] args)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environments.Production;
        var isDevelopment = environment == Environments.Development;
        var configuration = new ConfigurationBuilder();
        if (isDevelopment)
        {
            // Matches ServeFlow.Web.csproj; migration tools don't start the Web host.
            configuration.AddUserSecrets("serveflow-web-development");
        }
        configuration.AddEnvironmentVariables();

        // Model generation needs no server. Database update needs a reachable, explicitly configured instance.
        var connection = configuration.Build().GetConnectionString("ServeFlow")
            ?? "Server=localhost,1433;Database=ServeFlow;Integrated Security=true;Encrypt=true;TrustServerCertificate=false";
        connection = SqlServerConnection.Validate(connection, isDevelopment);
        return new ServeFlowDbContext(new DbContextOptionsBuilder<ServeFlowDbContext>()
            .UseSqlServer(connection).Options);
    }
}
