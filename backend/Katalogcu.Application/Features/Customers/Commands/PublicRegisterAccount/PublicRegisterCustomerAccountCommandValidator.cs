using FluentValidation;
using Katalogcu.Application.Features.Customers.Common;

namespace Katalogcu.Application.Features.Customers.Commands.PublicRegisterAccount;

public sealed class PublicRegisterCustomerAccountCommandValidator : AbstractValidator<PublicRegisterCustomerAccountCommand>
{
    public PublicRegisterCustomerAccountCommandValidator()
    {
        RuleFor(x => x.OwnerUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz public link.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Ad soyad zorunludur.");

        RuleFor(x => x.Phone)
            .Must(phone => !string.IsNullOrWhiteSpace(CustomerAuthHelpers.NormalizePhone(phone)))
            .WithMessage("Telefon zorunludur.");

        RuleFor(x => x.Password)
            .Must(CustomerAuthHelpers.IsPasswordStrong)
            .WithMessage("Şifre en az 8 karakter olmalıdır.");
    }
}
