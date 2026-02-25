using FluentValidation;

namespace Katalogcu.Application.Features.Hotspots.Commands.DetectHotspots;

public sealed class DetectHotspotsCommandValidator : AbstractValidator<DetectHotspotsCommand>
{
    public DetectHotspotsCommandValidator()
    {
        RuleFor(x => x.PageId)
            .NotEqual(Guid.Empty)
            .WithMessage("Sayfa bilgisi geçersiz.");
    }
}
