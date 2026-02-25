using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Commands.PublishCatalog;

public sealed class PublishCatalogCommandValidator : AbstractValidator<PublishCatalogCommand>
{
    public PublishCatalogCommandValidator()
    {
        RuleFor(x => x.CatalogId).NotEqual(Guid.Empty);
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
    }
}
