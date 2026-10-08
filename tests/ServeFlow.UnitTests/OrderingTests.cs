using ServeFlow.Application.Management;
using ServeFlow.Application.Ordering;

namespace ServeFlow.UnitTests;

public sealed class OrderingTests
{
    private static readonly Guid Item = Guid.NewGuid();
    private static readonly Guid Option = Guid.NewGuid();
    private static readonly string Version = Convert.ToBase64String(new byte[8]);
    private static MenuOffer Offer(bool available = true) => new(Item, Guid.NewGuid(), "Main", "Soup", "", 8.25m, Version, available, false,
        [new(Option, "Bread", 1.15m, Version)]);
    private static CartLine Line(int quantity = 1) => new(Item, Version, quantity, "  No salt  ", [new(Option, Version)]);
    private static PlaceOrderRequest Request() => new(Guid.NewGuid().ToString(), new string('a', 64), "Notes", [Line()]);

    [Fact]
    public void ServerPricingIncludesOptionsForEveryUnitAndTrimsInstructions()
    {
        var result = OrderCalculator.Calculate([Line(3), Line(2) with { Options = [] }], [Offer()]);
        Assert.True(result.Succeeded);
        Assert.Equal(44.70m, result.Total);
        Assert.Equal("No salt", result.Lines[0].Instructions);
        Assert.Equal("Soup", result.Lines[0].Name);
    }
    [Fact] public void UnavailableProductIsRejected() => Assert.False(OrderCalculator.Calculate([Line()], [Offer(false)]).Succeeded);
    [Fact] public void MissingProductIsRejected() => Assert.False(OrderCalculator.Calculate([Line()], []).Succeeded);
    [Fact] public void CrossProductOptionIsRejected() => Assert.False(OrderCalculator.Calculate([Line() with { Options = [new(Guid.NewGuid(), Version)] }], [Offer()]).Succeeded);
    [Fact] public void ChangedProductVersionRequiresReview() => Assert.False(OrderCalculator.Calculate([Line() with { Version = Convert.ToBase64String(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 }) }], [Offer()]).Succeeded);
    [Fact] public void ChangedOptionVersionRequiresReview() => Assert.False(OrderCalculator.Calculate([Line() with { Options = [new(Option, "changed")] }], [Offer()]).Succeeded);
    [Theory]
    [InlineData(0)] [InlineData(-1)] [InlineData(21)]
    public void InvalidQuantitiesFailValidation(int quantity) => Assert.False(new PlaceOrderValidator().Validate(Request() with { Lines = [Line(quantity)] }).IsValid);
    [Fact]
    public void DuplicateOptionsFailValidation() => Assert.False(new PlaceOrderValidator().Validate(Request() with { Lines = [Line() with { Options = [new(Option, Version), new(Option, Version)] }] }).IsValid);
    [Fact]
    public void MoreThanOneHundredUnitsFailValidation() => Assert.False(new PlaceOrderValidator().Validate(Request() with { Lines = Enumerable.Range(0, 6).Select(_ => Line(20)).ToArray() }).IsValid);
    [Fact] public void EmptyCartFailsValidation() => Assert.False(new PlaceOrderValidator().Validate(Request() with { Lines = [] }).IsValid);
    [Fact] public void ValidRequestPassesValidation() => Assert.True(new PlaceOrderValidator().Validate(Request()).IsValid);
    [Fact] public void LongInstructionsFailValidation() => Assert.False(new PlaceOrderValidator().Validate(Request() with { Lines = [Line() with { Instructions = new string('x', 501) }] }).IsValid);
    [Fact] public void MissingVersionFailsValidation() => Assert.False(new PlaceOrderValidator().Validate(Request() with { Lines = [Line() with { Version = "" }] }).IsValid);
    [Theory]
    [InlineData("")] [InlineData("guessable")] [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public void InvalidCapabilitiesFailValidation(string receipt) => Assert.False(new PlaceOrderValidator().Validate(Request() with { ReceiptToken = receipt }).IsValid);
    [Fact]
    public void MalformedNullCollectionsAndLinesFailWithoutThrowing()
    {
        var validator = new PlaceOrderValidator();
        Assert.False(validator.Validate(Request() with { Lines = null! }).IsValid);
        Assert.False(validator.Validate(Request() with { Lines = [null!] }).IsValid);
        Assert.False(validator.Validate(Request() with { Lines = [Line() with { Options = null! }] }).IsValid);
        Assert.False(validator.Validate(Request() with { Lines = [Line() with { Options = [null!] }] }).IsValid);
    }
    [Theory]
    [InlineData(-1)] [InlineData(10001)] [InlineData(1.001)]
    public void OptionPricesAreValidated(decimal price) => Assert.False(new OptionInputValidator().Validate(new OptionInput(null, Item, "Bread", price, true, false, null)).IsValid);
}
