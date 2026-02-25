using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Commands.CreateCatalog;

public sealed class CreateCatalogCommandValidator : AbstractValidator<CreateCatalogCommand>
{
    public CreateCatalogCommandValidator()
    {
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
        RuleFor(x => x.Name).NotEmpty().WithMessage("Katalog adı zorunludur.");
    }
}
