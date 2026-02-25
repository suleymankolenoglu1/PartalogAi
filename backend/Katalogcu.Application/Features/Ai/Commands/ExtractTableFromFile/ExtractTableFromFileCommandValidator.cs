using FluentValidation;

namespace Katalogcu.Application.Features.Ai.Commands.ExtractTableFromFile;

public sealed class ExtractTableFromFileCommandValidator : AbstractValidator<ExtractTableFromFileCommand>
{
    public ExtractTableFromFileCommandValidator()
    {
        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("Dosya yüklenmedi.");

        RuleFor(x => x.PageNumber)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Sayfa numarası geçersiz.");
    }
}
