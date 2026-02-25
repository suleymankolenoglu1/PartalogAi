using FluentValidation;
using Katalogcu.Application.Features.Customers.Common;

namespace Katalogcu.Application.Features.Customers.Commands.PublicLogin;

public sealed class PublicCustomerLoginCommandValidator : AbstractValidator<PublicCustomerLoginCommand>
{
    public PublicCustomerLoginCommandValidator()
    {
        RuleFor(x => x.OwnerUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz public link.");

        RuleFor(x => x)
            .Must(x =>
                !string.IsNullOrWhiteSpace(CustomerAuthHelpers.NormalizePhone(x.Phone)) ||
                !string.IsNullOrWhiteSpace(CustomerAuthHelpers.NormalizeEmail(x.Email)))
            .WithMessage("Telefon veya e-posta zorunludur.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Şifre zorunludur.");
    }
}
