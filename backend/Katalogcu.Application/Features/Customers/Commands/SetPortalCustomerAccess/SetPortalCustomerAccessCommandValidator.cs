using FluentValidation;

namespace Katalogcu.Application.Features.Customers.Commands.SetPortalCustomerAccess;

public sealed class SetPortalCustomerAccessCommandValidator : AbstractValidator<SetPortalCustomerAccessCommand>
{
    public SetPortalCustomerAccessCommandValidator()
    {
        RuleFor(x => x.OwnerUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz kullanıcı.");

        RuleFor(x => x.CustomerId)
            .NotEqual(Guid.Empty)
            .WithMessage("Müşteri zorunludur.");
    }
}
