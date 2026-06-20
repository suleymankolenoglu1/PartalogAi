using FluentValidation;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.RejectCatalogExternalMatch;

public sealed class RejectCatalogExternalMatchCommandValidator : AbstractValidator<RejectCatalogExternalMatchCommand>
{
    public RejectCatalogExternalMatchCommandValidator()
    {
        RuleFor(x => x.MatchId).NotEmpty();
        RuleFor(x => x.ReviewNote).MaximumLength(1024);
    }
}
