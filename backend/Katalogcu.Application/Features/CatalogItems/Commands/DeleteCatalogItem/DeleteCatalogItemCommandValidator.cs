using FluentValidation;

namespace Katalogcu.Application.Features.CatalogItems.Commands.DeleteCatalogItem;

public sealed class DeleteCatalogItemCommandValidator : AbstractValidator<DeleteCatalogItemCommand>
{
    public DeleteCatalogItemCommandValidator()
    {
        RuleFor(x => x.CatalogItemId).NotEqual(Guid.Empty);
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
    }
}
