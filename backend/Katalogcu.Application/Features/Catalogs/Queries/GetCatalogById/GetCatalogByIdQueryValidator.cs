using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetCatalogById;

public sealed class GetCatalogByIdQueryValidator : AbstractValidator<GetCatalogByIdQuery>
{
    public GetCatalogByIdQueryValidator()
    {
        RuleFor(x => x.CatalogId)
            .NotEqual(Guid.Empty)
            .WithMessage("Katalog bulunamadı.");

        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Kullanıcı bilgisi bulunamadı.");
    }
}
