using ServeFlow.Infrastructure.Configuration;

namespace ServeFlow.UnitTests;

public sealed class SqlServerConnectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Server=localhost")]
    [InlineData("not a connection string")]
    public void RejectsMissingOrMalformedConfiguration(string? connectionString)
    {
        Assert.Throws<InvalidOperationException>(() => SqlServerConnection.Validate(connectionString, true));
    }

    [Theory]
    [InlineData("Encrypt=false;TrustServerCertificate=false")]
    [InlineData("Encrypt=true;TrustServerCertificate=true")]
    public void ProductionRejectsUnverifiedTransport(string transport)
    {
        Assert.Throws<InvalidOperationException>(() => SqlServerConnection.Validate(
            $"Server=sql.example;Database=ServeFlow;{transport}", false));
    }

    [Fact]
    public void ProductionAcceptsVerifiedEncryptedTransport()
    {
        Assert.NotEmpty(SqlServerConnection.Validate(
            "Server=sql.example;Database=ServeFlow;Encrypt=true;TrustServerCertificate=false", false));
    }

    [Fact]
    public void ConfigurationFailureDoesNotExposeCredentials()
    {
        var error = Assert.Throws<InvalidOperationException>(() => SqlServerConnection.Validate(
            "Server=sql.example;Password=SensitiveValue;UnknownSetting=value", false));
        Assert.DoesNotContain("SensitiveValue", error.Message);
    }
}
