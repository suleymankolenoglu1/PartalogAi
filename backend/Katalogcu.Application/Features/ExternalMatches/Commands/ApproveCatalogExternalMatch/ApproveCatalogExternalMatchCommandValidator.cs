using FluentValidation;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.ApproveCatalogExternalMatch;

public sealed class ApproveCatalogExternalMatchCommandValidator : AbstractValidator<ApproveCatalogExternalMatchCommand>
{
    public ApproveCatalogExternalMatchCommandValidator()
    {
        RuleFor(x => x.MatchId).NotEmpty();
        RuleFor(x => x.ReviewNote).MaximumLength(1024);
    }
}
