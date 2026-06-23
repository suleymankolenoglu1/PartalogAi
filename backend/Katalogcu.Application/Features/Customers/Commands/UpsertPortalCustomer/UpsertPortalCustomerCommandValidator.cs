using FluentValidation;
using Katalogcu.Application.Features.Customers.Common;

namespace Katalogcu.Application.Features.Customers.Commands.UpsertPortalCustomer;

public sealed class UpsertPortalCustomerCommandValidator : AbstractValidator<UpsertPortalCustomerCommand>
{
    public UpsertPortalCustomerCommandValidator()
    {
        RuleFor(x => x.OwnerUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz kullanıcı.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Ad soyad zorunludur.");

        RuleFor(x => x.Phone)
            .Must(phone => !string.IsNullOrWhiteSpace(CustomerAuthHelpers.NormalizePhone(phone)))
            .WithMessage("Telefon zorunludur.");

        RuleFor(x => x.InitialPassword)
            .Must(password => string.IsNullOrWhiteSpace(password) || CustomerAuthHelpers.IsPasswordStrong(password))
            .WithMessage("Şifre en az 8 karakter olmalıdır.");
    }
}
