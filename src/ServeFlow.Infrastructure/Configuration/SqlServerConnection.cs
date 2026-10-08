using Microsoft.Data.SqlClient;

namespace ServeFlow.Infrastructure.Configuration;

public static class SqlServerConnection
{
    public static string Validate(string? connectionString, bool isDevelopment)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Set ConnectionStrings:ServeFlow using user secrets or ConnectionStrings__ServeFlow.");
        }

        SqlConnectionStringBuilder builder;
        try
        {
            builder = new SqlConnectionStringBuilder(connectionString);
        }
        catch (ArgumentException)
        {
            // Never include connection strings or credentials in an exception.
            throw new InvalidOperationException("ConnectionStrings:ServeFlow is not a valid SQL Server connection string.");
        }

        if (string.IsNullOrWhiteSpace(builder.DataSource) || string.IsNullOrWhiteSpace(builder.InitialCatalog))
        {
            throw new InvalidOperationException("SQL Server host and database name are required.");
        }

        if (!isDevelopment &&
            (builder.TrustServerCertificate || builder.Encrypt == SqlConnectionEncryptOption.Optional))
        {
            throw new InvalidOperationException("Production SQL Server connections require encryption and certificate validation.");
        }

        return builder.ConnectionString;
    }
}
