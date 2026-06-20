using FluentValidation;

namespace Katalogcu.Application.Features.ExternalLinks.Queries.GetPublishedExternalLinkByCatalogItem;

public sealed class GetPublishedExternalLinkByCatalogItemQueryValidator : AbstractValidator<GetPublishedExternalLinkByCatalogItemQuery>
{
    public GetPublishedExternalLinkByCatalogItemQueryValidator()
    {
        RuleFor(x => x.CatalogItemId).NotEmpty();
    }
}
