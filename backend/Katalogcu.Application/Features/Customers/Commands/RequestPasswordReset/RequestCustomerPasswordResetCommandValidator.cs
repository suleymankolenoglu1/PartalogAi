using FluentValidation;
using Katalogcu.Application.Features.Customers.Common;

namespace Katalogcu.Application.Features.Customers.Commands.RequestPasswordReset;

public sealed class RequestCustomerPasswordResetCommandValidator : AbstractValidator<RequestCustomerPasswordResetCommand>
{
    public RequestCustomerPasswordResetCommandValidator()
    {
        RuleFor(x => x.OwnerUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz public link.");

        RuleFor(x => x)
            .Must(x =>
                !string.IsNullOrWhiteSpace(CustomerAuthHelpers.NormalizePhone(x.Phone)) ||
                !string.IsNullOrWhiteSpace(CustomerAuthHelpers.NormalizeEmail(x.Email)))
            .WithMessage("Telefon veya e-posta zorunludur.");
    }
}
