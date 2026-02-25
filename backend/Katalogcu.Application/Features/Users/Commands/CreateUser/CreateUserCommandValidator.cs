using FluentValidation;

namespace Katalogcu.Application.Features.Users.Commands.CreateUser;

public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("Ad zorunludur.");

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
