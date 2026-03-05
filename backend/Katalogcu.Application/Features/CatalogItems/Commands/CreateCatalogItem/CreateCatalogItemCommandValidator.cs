using FluentValidation;

namespace Katalogcu.Application.Features.CatalogItems.Commands.CreateCatalogItem;

public sealed class CreateCatalogItemCommandValidator : AbstractValidator<CreateCatalogItemCommand>
{
    public CreateCatalogItemCommandValidator()
    {
        RuleFor(x => x.CatalogId).NotEqual(Guid.Empty);
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
        RuleFor(x => x.PageNumber).GreaterThan(0);

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
