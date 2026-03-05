using FluentValidation;

namespace Katalogcu.Application.Features.CatalogItems.Commands.UpdateCatalogItem;

public sealed class UpdateCatalogItemCommandValidator : AbstractValidator<UpdateCatalogItemCommand>
{
    public UpdateCatalogItemCommandValidator()
    {
        RuleFor(x => x.CatalogItemId).NotEqual(Guid.Empty);
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);

        RuleFor(x => x.RefNo)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.PartCode)
            .NotEmpty()
            .MaximumLength(256);

        RuleFor(x => x.PartName)
            .NotEmpty()
            .MaximumLength(512);

        RuleFor(x => x.Description)
            .MaximumLength(2048);
    }
}
