using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Commands.DeleteCatalog;

public sealed class DeleteCatalogCommandValidator : AbstractValidator<DeleteCatalogCommand>
{
    public DeleteCatalogCommandValidator()
    {
        RuleFor(x => x.CatalogId).NotEqual(Guid.Empty);
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
    }
}
