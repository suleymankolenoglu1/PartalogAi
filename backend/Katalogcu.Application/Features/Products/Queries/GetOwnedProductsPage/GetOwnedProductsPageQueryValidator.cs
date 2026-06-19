using FluentValidation;

namespace Katalogcu.Application.Features.Products.Queries.GetOwnedProductsPage;

public sealed class GetOwnedProductsPageQueryValidator : AbstractValidator<GetOwnedProductsPageQuery>
{
    public GetOwnedProductsPageQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Search).MaximumLength(120);
        RuleFor(x => x.StockStatus)
            .Must(x => string.IsNullOrWhiteSpace(x) || x is "low" or "out")
            .WithMessage("Geçersiz stok filtresi.");
    }
}
