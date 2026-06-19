using FluentValidation;

namespace Katalogcu.Application.Features.Customers.Commands.CreatePublicCustomerMachine;

public sealed class CreatePublicCustomerMachineCommandValidator : AbstractValidator<CreatePublicCustomerMachineCommand>
{
    public CreatePublicCustomerMachineCommandValidator()
    {
        RuleFor(x => x.OwnerUserId).NotEmpty();
        RuleFor(x => x.SessionToken).NotEmpty();
        RuleFor(x => x.Brand).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Model).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Variant).MaximumLength(128);
        RuleFor(x => x.MachineGroup).MaximumLength(128);
        RuleFor(x => x.SerialNumber).MaximumLength(128);
        RuleFor(x => x.DisplayName).MaximumLength(256);
    }
}
