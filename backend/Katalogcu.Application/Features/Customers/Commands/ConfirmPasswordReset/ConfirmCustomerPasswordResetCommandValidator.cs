using FluentValidation;
using Katalogcu.Application.Features.Customers.Common;

namespace Katalogcu.Application.Features.Customers.Commands.ConfirmPasswordReset;

public sealed class ConfirmCustomerPasswordResetCommandValidator : AbstractValidator<ConfirmCustomerPasswordResetCommand>
{
    public ConfirmCustomerPasswordResetCommandValidator()
    {
        RuleFor(x => x.OwnerUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz public link.");

        RuleFor(x => x)
            .Must(x =>
                !string.IsNullOrWhiteSpace(CustomerAuthHelpers.NormalizePhone(x.Phone)) ||
                !string.IsNullOrWhiteSpace(CustomerAuthHelpers.NormalizeEmail(x.Email)))
            .WithMessage("Telefon veya e-posta zorunludur.");

        RuleFor(x => x.ResetCode)
            .NotEmpty()
            .WithMessage("Doğrulama kodu zorunludur.");

        RuleFor(x => x.NewPassword)
            .Must(CustomerAuthHelpers.IsPasswordStrong)
            .WithMessage("Yeni şifre en az 8 karakter olmalıdır.");
    }
}
