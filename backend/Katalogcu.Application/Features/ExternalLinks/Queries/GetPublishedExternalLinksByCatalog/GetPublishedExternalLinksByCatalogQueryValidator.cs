using FluentValidation;

namespace Katalogcu.Application.Features.ExternalLinks.Queries.GetPublishedExternalLinksByCatalog;

public sealed class GetPublishedExternalLinksByCatalogQueryValidator : AbstractValidator<GetPublishedExternalLinksByCatalogQuery>
{
    public GetPublishedExternalLinksByCatalogQueryValidator()
    {
        RuleFor(x => x.CatalogId).NotEmpty();
    }
}
