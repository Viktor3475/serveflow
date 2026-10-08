using Microsoft.Extensions.Diagnostics.HealthChecks;
using ServeFlow.Infrastructure.Persistence;

namespace ServeFlow.Web.Health;

public sealed class SqlServerHealthCheck(ServeFlowDbContext dbContext) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var connected = await dbContext.Database.CanConnectAsync(cancellationToken);
        return connected
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy("SQL Server is unavailable.");
    }
}
