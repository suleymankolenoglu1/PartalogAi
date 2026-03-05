using FluentValidation;

namespace Katalogcu.Application.Features.Hotspots.Commands.UpdateHotspot;

public sealed class UpdateHotspotCommandValidator : AbstractValidator<UpdateHotspotCommand>
{
    public UpdateHotspotCommandValidator()
    {
        RuleFor(x => x.HotspotId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz hotspot.");

        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Kullanıcı bulunamadı.");

        RuleFor(x => x.Left).InclusiveBetween(0, 100);
        RuleFor(x => x.Top).InclusiveBetween(0, 100);
        RuleFor(x => x.Width).GreaterThan(0).LessThanOrEqualTo(100);
        RuleFor(x => x.Height).GreaterThan(0).LessThanOrEqualTo(100);
    }
}
