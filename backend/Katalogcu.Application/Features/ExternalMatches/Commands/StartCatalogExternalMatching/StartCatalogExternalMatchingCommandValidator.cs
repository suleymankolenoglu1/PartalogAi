using FluentValidation;

namespace Katalogcu.Application.Features.ExternalMatches.Commands.StartCatalogExternalMatching;

public sealed class StartCatalogExternalMatchingCommandValidator : AbstractValidator<StartCatalogExternalMatchingCommand>
{
    public StartCatalogExternalMatchingCommandValidator()
    {
        RuleFor(x => x.CatalogId).NotEmpty();
        RuleFor(x => x.ExternalSiteId).NotEmpty();
    }
}
