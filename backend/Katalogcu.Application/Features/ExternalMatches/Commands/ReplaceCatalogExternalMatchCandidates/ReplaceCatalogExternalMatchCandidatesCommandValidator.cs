using FluentValidation;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.ReplaceCatalogExternalMatchCandidates;

public sealed class ReplaceCatalogExternalMatchCandidatesCommandValidator : AbstractValidator<ReplaceCatalogExternalMatchCandidatesCommand>
{
    public ReplaceCatalogExternalMatchCandidatesCommandValidator()
    {
        RuleFor(x => x.CatalogId).NotEmpty();
        RuleFor(x => x.ExternalSiteId).NotEmpty();
    }
}
