using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetMyCatalogsPage;

public sealed class GetMyCatalogsPageQueryValidator : AbstractValidator<GetMyCatalogsPageQuery>
{
    public GetMyCatalogsPageQueryValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(120);
    }
}
