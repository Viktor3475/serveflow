using ServeFlow.Application.Management;
using ServeFlow.Domain.Enums;

namespace ServeFlow.UnitTests;

public sealed class ManagementValidationTests
{
    private static string Version => Convert.ToBase64String(new byte[8]);
    [Theory]
    [InlineData(-1)]
    [InlineData(1.001)]
    [InlineData(1000001)]
    public async Task MenuPricesRejectNegativeExcessPrecisionAndUnboundedValues(double price)
    {
        var result = await new MenuValidator().ValidateAsync(new MenuInput(null, Guid.NewGuid(), "Soup", "", (decimal)price, true, false, null), TestContext.Current.CancellationToken);
        Assert.Contains(result.Errors, x => x.PropertyName == "Price");
    }
    [Fact]
    public async Task CategoryAndVersionAreRequiredForMenuEdits()
    {
        var result = await new MenuValidator().ValidateAsync(new MenuInput(Guid.NewGuid(), Guid.Empty, "Soup", "", 12.50m, true, false, "invalid"), TestContext.Current.CancellationToken);
        Assert.Contains(result.Errors, x => x.PropertyName == "CategoryId");
        Assert.Contains(result.Errors, x => x.PropertyName == "Version");
    }
    [Fact]
    public async Task NewProductCanHaveAnEmptyDescriptionAndNoVersion()
    {
        var result = await new MenuValidator().ValidateAsync(new MenuInput(null, Guid.NewGuid(), "Soup", "", 12.50m, true, false, null), TestContext.Current.CancellationToken);
        Assert.True(result.IsValid);
    }
    [Theory]
    [InlineData(0, 4, 0)]
    [InlineData(1, 101, 0)]
    [InlineData(1, 4, 999)]
    public async Task TablesRejectInvalidNumberCapacityAndStatus(int number, int capacity, int status)
    {
        var result = await new TableValidator().ValidateAsync(new TableInput(null, number, capacity, (TableStatus)status, false, null), TestContext.Current.CancellationToken);
        Assert.False(result.IsValid);
    }
    [Fact]
    public async Task CategoryWhitespaceNamesAndNegativeDisplayOrderAreRejected()
    {
        var result = await new CategoryValidator().ValidateAsync(new CategoryInput(null, "   ", -1, false, null), TestContext.Current.CancellationToken);
        Assert.False(result.IsValid);
    }
    [Fact]
    public async Task InvalidTimeZoneIsRejected()
    {
        var result = await new SettingsValidator().ValidateAsync(new SettingsInput("Restaurant", "Not/AZone", Version), TestContext.Current.CancellationToken);
        Assert.Contains(result.Errors, x => x.PropertyName == "TimeZoneId");
    }
    [Theory]
    [InlineData(null)]
    [InlineData("AAAAAAAA")]
    [InlineData("not_base64!!")]
    [InlineData("AAAAAAAAAAAA")]
    public void VersionMustDecodeToExactlyEightBytes(string? value) => Assert.Null(ConcurrencyVersion.Decode(value));
}
