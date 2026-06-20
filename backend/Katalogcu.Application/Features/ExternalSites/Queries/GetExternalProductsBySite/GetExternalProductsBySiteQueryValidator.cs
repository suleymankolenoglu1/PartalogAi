using FluentValidation;

namespace Katalogcu.Application.Features.ExternalSites.Queries.GetExternalProductsBySite;

public sealed class GetExternalProductsBySiteQueryValidator : AbstractValidator<GetExternalProductsBySiteQuery>
{
    public GetExternalProductsBySiteQueryValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 200);
    }
}
