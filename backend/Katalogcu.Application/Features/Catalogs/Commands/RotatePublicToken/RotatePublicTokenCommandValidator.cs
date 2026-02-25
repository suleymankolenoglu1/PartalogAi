using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Commands.RotatePublicToken;

public sealed class RotatePublicTokenCommandValidator : AbstractValidator<RotatePublicTokenCommand>
{
    public RotatePublicTokenCommandValidator()
    {
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
    }
}
