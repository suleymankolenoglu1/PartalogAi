using FluentValidation;

namespace Katalogcu.Application.Features.Customers.Commands.DeletePublicCustomerMachine;

public sealed class DeletePublicCustomerMachineCommandValidator : AbstractValidator<DeletePublicCustomerMachineCommand>
{
    public DeletePublicCustomerMachineCommandValidator()
    {
        RuleFor(x => x.OwnerUserId).NotEmpty();
        RuleFor(x => x.SessionToken).NotEmpty();
        RuleFor(x => x.MachineId).NotEmpty();
    }
}
