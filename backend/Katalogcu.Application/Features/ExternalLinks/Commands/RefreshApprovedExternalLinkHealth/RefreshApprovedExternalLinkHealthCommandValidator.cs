using FluentValidation;

namespace Katalogcu.Application.Features.ExternalLinks.Commands.RefreshApprovedExternalLinkHealth;

public sealed class RefreshApprovedExternalLinkHealthCommandValidator : AbstractValidator<RefreshApprovedExternalLinkHealthCommand>
{
    public RefreshApprovedExternalLinkHealthCommandValidator()
    {
        RuleFor(x => x.MatchId).NotEmpty();
    }
}
