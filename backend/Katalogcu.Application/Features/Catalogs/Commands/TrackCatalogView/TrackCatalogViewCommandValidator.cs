using FluentValidation;

namespace Katalogcu.Application.Features.Catalogs.Commands.TrackCatalogView;

public sealed class TrackCatalogViewCommandValidator : AbstractValidator<TrackCatalogViewCommand>
{
    public TrackCatalogViewCommandValidator()
    {
        RuleFor(x => x.CatalogId)
            .NotEqual(Guid.Empty)
            .WithMessage("Katalog ID zorunludur.");

        RuleFor(x => x.OwnerUserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Kullanıcı ID zorunludur.");

        RuleFor(x => x.FingerprintHash)
            .NotEmpty()
            .MaximumLength(128)
            .WithMessage("Fingerprint zorunludur.");

        RuleFor(x => x.Source)
            .NotEmpty()
            .MaximumLength(64)
            .WithMessage("Kaynak bilgisi zorunludur.");
    }
}
