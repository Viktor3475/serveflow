using FluentValidation;
using ServeFlow.Domain.Enums;

namespace ServeFlow.Application.Management;

public static class ConcurrencyVersion
{
    public static byte[]? Decode(string? version)
    {
        if (version is null || version.Length != 12) return null;
        try { var bytes = Convert.FromBase64String(version); return bytes.Length == 8 ? bytes : null; }
        catch (FormatException) { return null; }
    }
}

public sealed class SettingsValidator : AbstractValidator<SettingsInput>
{
    public SettingsValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.TimeZoneId).NotEmpty().MaximumLength(100).Must(IsTimeZone).WithMessage("Select a valid time zone.");
        RuleFor(x => x.Version).Must(x => ConcurrencyVersion.Decode(x) is not null).WithMessage("Reload the settings before saving.");
    }
    private static bool IsTimeZone(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { TimeZoneInfo.FindSystemTimeZoneById(value); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }
}

public sealed class CategoryValidator : AbstractValidator<CategoryInput>
{
    public CategoryValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SortOrder).InclusiveBetween(0, 10000);
        When(x => x.Id.HasValue, () =>
        {
            RuleFor(x => x.Id).NotEqual(Guid.Empty);
            RuleFor(x => x.Version).Must(x => ConcurrencyVersion.Decode(x) is not null).WithMessage("Reload the category before saving.");
        });
    }
}

public sealed class MenuValidator : AbstractValidator<MenuInput>
{
    public MenuValidator()
    {
        RuleFor(x => x.CategoryId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Description).NotNull().MaximumLength(2000);
        RuleFor(x => x.Price).InclusiveBetween(0, 1000000).Must(x => decimal.Round(x, 2) == x).WithMessage("Use a price with at most two decimal places.");
        When(x => x.Id.HasValue, () =>
        {
            RuleFor(x => x.Id).NotEqual(Guid.Empty);
            RuleFor(x => x.Version).Must(x => ConcurrencyVersion.Decode(x) is not null).WithMessage("Reload the product before saving.");
        });
    }
}

public sealed class TableValidator : AbstractValidator<TableInput>
{
    public TableValidator()
    {
        RuleFor(x => x.Number).InclusiveBetween(1, 10000);
        RuleFor(x => x.Capacity).InclusiveBetween(1, 100);
        RuleFor(x => x.Status).IsInEnum();
        When(x => x.Id.HasValue, () =>
        {
            RuleFor(x => x.Id).NotEqual(Guid.Empty);
            RuleFor(x => x.Version).Must(x => ConcurrencyVersion.Decode(x) is not null).WithMessage("Reload the table before saving.");
        });
    }
}
