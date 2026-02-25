using FluentValidation;

namespace Katalogcu.Application.Features.Hotspots.Commands.DeleteHotspot;

public sealed class DeleteHotspotCommandValidator : AbstractValidator<DeleteHotspotCommand>
{
    public DeleteHotspotCommandValidator()
    {
        RuleFor(x => x.HotspotId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz hotspot.");
    }
}
