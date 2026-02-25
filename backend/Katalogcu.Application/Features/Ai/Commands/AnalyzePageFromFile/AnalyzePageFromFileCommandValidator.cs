using FluentValidation;

namespace Katalogcu.Application.Features.Ai.Commands.AnalyzePageFromFile;

public sealed class AnalyzePageFromFileCommandValidator : AbstractValidator<AnalyzePageFromFileCommand>
{
    public AnalyzePageFromFileCommandValidator()
    {
        RuleFor(x => x.File)
            .NotNull()
            .WithMessage("Dosya yüklenmedi.");
    }
}
