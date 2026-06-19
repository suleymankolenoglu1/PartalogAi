using FluentValidation;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.BulkApproveCatalogExternalMatches;

public sealed class BulkApproveCatalogExternalMatchesCommandValidator : AbstractValidator<BulkApproveCatalogExternalMatchesCommand>
{
    public BulkApproveCatalogExternalMatchesCommandValidator()
    {
        RuleFor(x => x.MatchIds).NotEmpty();
        RuleForEach(x => x.MatchIds).NotEmpty();
        RuleFor(x => x.ReviewNote).MaximumLength(1024);
    }
}
