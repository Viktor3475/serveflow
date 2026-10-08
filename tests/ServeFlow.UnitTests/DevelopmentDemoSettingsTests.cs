using Microsoft.Extensions.Configuration;
using ServeFlow.Infrastructure.Identity;

namespace ServeFlow.UnitTests;

public sealed class DevelopmentDemoSettingsTests
{
    [Fact]
    public void ProductionRejectsDemoBeforeReadingCredentials()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => DevelopmentDemoSettings.Read(new ConfigurationBuilder().Build(), false));
        Assert.Contains("only in Development", exception.Message);
    }
    [Theory]
    [InlineData("Demo:WaiterPassword")]
    [InlineData("Demo:KitchenPassword")]
    [InlineData("Bootstrap:RestaurantId")]
    public void MissingExternalValuesFailWithKeyOnly(string key)
    {
        var values = Values(); values[key] = null;
        var exception = Assert.Throws<InvalidOperationException>(() => DevelopmentDemoSettings.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), true));
        Assert.Contains(key, exception.Message); Assert.DoesNotContain("Strong", exception.Message);
    }
    [Fact]
    public void EmailCollisionIsRejectedWithoutExposingAddresses()
    {
        var values = Values(); values["Demo:WaiterEmail"] = "ROOT@example.test";
        var exception = Assert.Throws<InvalidOperationException>(() => DevelopmentDemoSettings.Read(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), true));
        Assert.Contains("distinct", exception.Message); Assert.DoesNotContain("example.test", exception.Message);
    }
    [Fact]
    public void DevelopmentReadsProvidedCredentialsWithoutDefaults()
    {
        var result = DevelopmentDemoSettings.Read(new ConfigurationBuilder().AddInMemoryCollection(Values()).Build(), true);
        Assert.Equal("waiter@example.test", result.WaiterEmail); Assert.Equal("Test-only-Strong42!", result.KitchenPassword);
    }
    private static Dictionary<string, string?> Values() => new()
    {
        ["Bootstrap:RestaurantId"] = Guid.NewGuid().ToString(), ["Bootstrap:Email"] = "root@example.test",
        ["Demo:WaiterEmail"] = "waiter@example.test", ["Demo:WaiterPassword"] = "Test-only-Strong42!",
        ["Demo:KitchenEmail"] = "kitchen@example.test", ["Demo:KitchenPassword"] = "Test-only-Strong42!"
    };
}
