using FluentValidation;

namespace Katalogcu.Application.Features.ExternalMatches.Queries.GetCatalogApprovedExternalMatches;

public sealed class GetCatalogApprovedExternalMatchesQueryValidator : AbstractValidator<GetCatalogApprovedExternalMatchesQuery>
{
    public GetCatalogApprovedExternalMatchesQueryValidator()
    {
        RuleFor(x => x.CatalogId).NotEmpty();
    }
}
