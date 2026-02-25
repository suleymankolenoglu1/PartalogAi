using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Commands.ProcessCatalogPages;

public sealed class ProcessCatalogPagesCommandValidator : AbstractValidator<ProcessCatalogPagesCommand>
{
    public ProcessCatalogPagesCommandValidator()
    {
        RuleFor(x => x.CatalogId).NotEqual(Guid.Empty);
    }
}
