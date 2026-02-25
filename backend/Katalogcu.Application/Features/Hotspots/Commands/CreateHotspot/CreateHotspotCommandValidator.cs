using FluentValidation;

namespace Katalogcu.Application.Features.Hotspots.Commands.CreateHotspot;

public sealed class CreateHotspotCommandValidator : AbstractValidator<CreateHotspotCommand>
{
    public CreateHotspotCommandValidator()
    {
        RuleFor(x => x.PageId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz veri.");
    }
}
