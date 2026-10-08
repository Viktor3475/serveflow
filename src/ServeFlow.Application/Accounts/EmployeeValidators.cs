using FluentValidation;
using ServeFlow.Application.Security;

namespace ServeFlow.Application.Accounts;

public sealed class CreateEmployeeValidator : AbstractValidator<CreateEmployeeRequest>
{
    public CreateEmployeeValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(256);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128);
        RuleFor(x => x.Role).Must(role => StaffRoles.EmployeeRoles.Contains(role))
            .WithMessage("Select a valid staff role.");
    }
}

public sealed class EditEmployeeValidator : AbstractValidator<EditEmployeeRequest>
{
    public EditEmployeeValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(160);
        RuleFor(x => x.ConcurrencyStamp).NotEmpty().MaximumLength(64);
        RuleFor(x => x.Role).Must(role => StaffRoles.EmployeeRoles.Contains(role))
            .WithMessage("Select a valid staff role.");
    }
}
