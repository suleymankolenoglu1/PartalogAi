using FluentValidation;

namespace Katalogcu.Application.Features.Auth.Commands.UpdateMe;

public sealed class UpdateMeCommandValidator : AbstractValidator<UpdateMeCommand>
{
    public UpdateMeCommandValidator()
    {
        RuleFor(x => x.FirstName)
            .NotEmpty()
            .WithMessage("Ad zorunludur.");

        RuleFor(x => x.LastName)
            .NotEmpty()
            .WithMessage("Soyad zorunludur.");
    }
}
