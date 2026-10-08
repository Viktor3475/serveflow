using FluentValidation;
using ServeFlow.Application.Accounts;

namespace ServeFlow.Application.Management;

public sealed record OptionDetails(Guid Id, Guid ItemId, string Name, decimal Price, bool Available, bool Archived, string Version);
public sealed record OptionInput(Guid? Id, Guid ItemId, string Name, decimal Price, bool Available, bool Archived, string? Version);
public interface IMenuOptionService
{
    Task<ManagementRead<IReadOnlyList<OptionDetails>>> ListAsync(Guid actor, Guid item, CancellationToken token);
    Task<OperationResult> SaveAsync(Guid actor, OptionInput input, CancellationToken token);
}
public sealed class OptionInputValidator : AbstractValidator<OptionInput>
{
    public OptionInputValidator()
    {
        RuleFor(x => x.ItemId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().Must(x => !string.IsNullOrWhiteSpace(x)).MaximumLength(100);
        RuleFor(x => x.Price).InclusiveBetween(0, 10000).Must(x => decimal.Round(x, 2) == x);
        When(x => x.Id is not null, () =>
        {
            RuleFor(x => x.Id).NotEqual(Guid.Empty);
            RuleFor(x => x.Version).Must(x => ConcurrencyVersion.Decode(x) is not null);
        });
    }
}
