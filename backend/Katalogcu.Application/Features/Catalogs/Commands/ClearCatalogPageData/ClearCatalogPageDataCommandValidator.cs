using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Commands.ClearCatalogPageData;

public sealed class ClearCatalogPageDataCommandValidator : AbstractValidator<ClearCatalogPageDataCommand>
{
    public ClearCatalogPageDataCommandValidator()
    {
        RuleFor(x => x.CatalogId).NotEqual(Guid.Empty);
        RuleFor(x => x.PageId).NotEqual(Guid.Empty);
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
    }
}
