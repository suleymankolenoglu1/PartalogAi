using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Commands.StartCatalogAiProcess;

public sealed class StartCatalogAiProcessCommandValidator : AbstractValidator<StartCatalogAiProcessCommand>
{
    public StartCatalogAiProcessCommandValidator()
    {
        RuleFor(x => x.CatalogId).NotEqual(Guid.Empty);
        RuleFor(x => x.UserId).NotEqual(Guid.Empty);
    }
}
