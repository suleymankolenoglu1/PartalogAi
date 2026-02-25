using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetCatalogPageItems;

public sealed class GetCatalogPageItemsQueryValidator : AbstractValidator<GetCatalogPageItemsQuery>
{
    public GetCatalogPageItemsQueryValidator()
    {
        RuleFor(x => x.CatalogId).NotEqual(Guid.Empty).WithMessage("Katalog bilgisi geçersiz.");
        RuleFor(x => x.UserId).NotEqual(Guid.Empty).WithMessage("Kullanıcı bilgisi bulunamadı.");
        RuleFor(x => x.PageNumber).GreaterThan(0).WithMessage("Sayfa numarası geçersiz.");
    }
}
