using FluentValidation;

namespace Katalogcu.Application.Features.ExternalMatches.Queries.GetCatalogExternalMatchQueue;

public sealed class GetCatalogExternalMatchQueueQueryValidator : AbstractValidator<GetCatalogExternalMatchQueueQuery>
{
    public GetCatalogExternalMatchQueueQueryValidator()
    {
        RuleFor(x => x.CatalogId).NotEmpty();
    }
}
