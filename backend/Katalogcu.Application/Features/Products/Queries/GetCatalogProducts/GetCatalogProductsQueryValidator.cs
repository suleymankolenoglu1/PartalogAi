using FluentValidation;

namespace Katalogcu.Application.Features.Products.Queries.GetCatalogProducts;

public sealed class GetCatalogProductsQueryValidator : AbstractValidator<GetCatalogProductsQuery>
{
    public GetCatalogProductsQueryValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Kullanıcı bilgisi bulunamadı.");

        RuleFor(x => x.CatalogId)
            .NotEqual(Guid.Empty)
            .WithMessage("Katalog bilgisi geçersiz.");
    }
}
