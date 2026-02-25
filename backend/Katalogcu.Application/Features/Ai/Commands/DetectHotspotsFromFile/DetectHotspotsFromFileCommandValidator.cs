using FluentValidation;

namespace Katalogcu.Application.Features.Ai.Commands.DetectHotspotsFromFile;

public sealed class DetectHotspotsFromFileCommandValidator : AbstractValidator<DetectHotspotsFromFileCommand>
{
    public DetectHotspotsFromFileCommandValidator()
    {
        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("Dosya yüklenmedi.");

        RuleFor(x => x.PageId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçersiz sayfa bilgisi.");
    }
}
