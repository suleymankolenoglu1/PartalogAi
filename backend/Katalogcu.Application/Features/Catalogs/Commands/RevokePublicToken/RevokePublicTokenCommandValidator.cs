using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Commands.RevokePublicToken;

public sealed class RevokePublicTokenCommandValidator : AbstractValidator<RevokePublicTokenCommand>
{
    public RevokePublicTokenCommandValidator()
    {
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
    }
}
