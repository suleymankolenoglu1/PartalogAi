using FluentValidation;

namespace Katalogcu.Application.Features.ExternalLinks.Commands.RestoreApprovedExternalMatch;

public sealed class RestoreApprovedExternalMatchCommandValidator : AbstractValidator<RestoreApprovedExternalMatchCommand>
{
    public RestoreApprovedExternalMatchCommandValidator()
    {
        RuleFor(x => x.MatchId).NotEmpty();
    }
}
