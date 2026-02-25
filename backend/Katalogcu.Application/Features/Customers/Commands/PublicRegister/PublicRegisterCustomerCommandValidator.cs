using FluentValidation;
using Katalogcu.Application.Features.Customers.Common;

namespace Katalogcu.Application.Features.Customers.Commands.PublicRegister;

public sealed class PublicRegisterCustomerCommandValidator : AbstractValidator<PublicRegisterCustomerCommand>
{
    public PublicRegisterCustomerCommandValidator()
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
    }
}
