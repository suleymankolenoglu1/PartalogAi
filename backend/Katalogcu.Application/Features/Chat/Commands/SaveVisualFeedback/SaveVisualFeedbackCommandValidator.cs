using FluentValidation;

namespace Katalogcu.Application.Features.Chat.Commands.SaveVisualFeedback;

public sealed class SaveVisualFeedbackCommandValidator : AbstractValidator<SaveVisualFeedbackCommand>
{
    public SaveVisualFeedbackCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçerli kullanıcı veya public token gerekli.");

        RuleFor(x => x.ImageBytes)
            .NotNull()
            .Must(x => x.Length > 0)
            .WithMessage("Fotoğraf zorunlu.");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.PartName) || !string.IsNullOrWhiteSpace(x.PartCode))
            .WithMessage("partName veya partCode zorunlu.");
    }
}
