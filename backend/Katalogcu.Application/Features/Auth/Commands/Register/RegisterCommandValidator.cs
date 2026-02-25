using FluentValidation;

namespace Katalogcu.Application.Features.Auth.Commands.Register;

public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
{
    public RegisterCommandValidator()
    {
        RuleFor(x => x.FullName)
            .NotEmpty()
            .WithMessage("Ad soyad zorunludur.");

        RuleFor(x => x.Email)
            .NotEmpty()
            .WithMessage("Email zorunludur.");

        RuleFor(x => x.Password)
            .NotEmpty()
            .WithMessage("Şifre zorunludur.");

        RuleFor(x => x.Password)
            .MinimumLength(8)
            .WithMessage("Şifre en az 8 karakter olmalıdır.");
    }
}
