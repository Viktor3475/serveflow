using FluentValidation;
using ServeFlow.Application.Management;

namespace ServeFlow.Application.Ordering;

public sealed class PlaceOrderValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderValidator()
    {
        RuleFor(x => x.IdempotencyKey).Must(x => Guid.TryParseExact(x, "D", out var value) && value != Guid.Empty && value.ToString("D") == x);
        RuleFor(x => x.ReceiptToken).Must(CapabilityTokens.Valid).WithMessage("Use a 64-character lowercase hexadecimal receipt token.");
        RuleFor(x => x.Notes).MaximumLength(1000);
        RuleFor(x => x.Lines).NotNull().Must(x => x is { Count: >= 1 and <= 50 }).WithMessage("Choose between 1 and 50 cart lines.");
        RuleForEach(x => x.Lines).NotNull().SetValidator(new CartLineValidator());
        RuleFor(x => x.Lines).Must(x => x is not null && x.Sum(line => line is null ? 101L : (long)line.Quantity) <= 100).WithMessage("A single order can contain at most 100 items.");
    }
}
public sealed class CartLineValidator : AbstractValidator<CartLine>
{
    public CartLineValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Version).Must(x => ConcurrencyVersion.Decode(x) is not null);
        RuleFor(x => x.Quantity).InclusiveBetween(1, 20);
        RuleFor(x => x.Instructions).MaximumLength(500);
        RuleFor(x => x.Options).NotNull().Must(x => x is { Count: <= 10 } && x.Select(o => o?.Id).Distinct().Count() == x.Count);
        RuleForEach(x => x.Options).NotNull().ChildRules(option =>
        {
            option.RuleFor(x => x.Id).NotEmpty();
            option.RuleFor(x => x.Version).Must(x => ConcurrencyVersion.Decode(x) is not null);
        });
    }
}
public static class CapabilityTokens
{
    // 32 bytes represented as lowercase hex; never store or log the raw value.
    public static bool Valid(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
public static class OrderCalculator
{
    public static PricingResult Calculate(IReadOnlyList<CartLine> lines, IReadOnlyList<MenuOffer> menu)
    {
        var priced = new List<PricedLine>();
        foreach (var line in lines)
        {
            var item = menu.SingleOrDefault(x => x.Id == line.ItemId);
            if (item is null || !item.Available) return new([], "A selected product is no longer available. Refresh the menu.");
            if (item.Version != line.Version) return new([], "A product changed. Refresh the menu and review your cart.");
            var options = new List<OptionOffer>();
            foreach (var choice in line.Options)
            {
                var option = item.Options.SingleOrDefault(x => x.Id == choice.Id);
                if (option is null || option.Version != choice.Version) return new([], "A selected option changed or is unavailable. Refresh the menu.");
                options.Add(option);
            }
            priced.Add(new(item.Id, item.Name, item.Price, line.Quantity, line.Instructions?.Trim(), options));
        }
        var result = new PricingResult(priced, null);
        return result.Total > 1000000m ? new([], "Order total exceeds the permitted limit.") : result;
    }
}
