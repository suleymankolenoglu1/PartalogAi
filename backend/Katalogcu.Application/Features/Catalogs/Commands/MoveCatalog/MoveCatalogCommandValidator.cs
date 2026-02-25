using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Commands.MoveCatalog;

public sealed class MoveCatalogCommandValidator : AbstractValidator<MoveCatalogCommand>
{
    public MoveCatalogCommandValidator()
    {
        RuleFor(x => x.CatalogId)
            .NotEqual(Guid.Empty)
            .WithMessage("Katalog bilgisi geçersiz.");

        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz kullanıcı.");
    }
}
