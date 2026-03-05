using FluentValidation;

namespace Katalogcu.Application.Features.Hotspots.Commands.CreateHotspot;

public sealed class CreateHotspotCommandValidator : AbstractValidator<CreateHotspotCommand>
{
    public CreateHotspotCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Kullanıcı bulunamadı.");

        RuleFor(x => x.PageId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz veri.");

        RuleFor(x => x.Left).InclusiveBetween(0, 100);
        RuleFor(x => x.Top).InclusiveBetween(0, 100);
        RuleFor(x => x.Width).GreaterThan(0).LessThanOrEqualTo(100);
        RuleFor(x => x.Height).GreaterThan(0).LessThanOrEqualTo(100);
    }
}
