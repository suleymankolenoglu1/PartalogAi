using FluentValidation;

namespace Katalogcu.Application.Features.Chat.Commands.SaveChatFeedback;

public sealed class SaveChatFeedbackCommandValidator : AbstractValidator<SaveChatFeedbackCommand>
{
    public SaveChatFeedbackCommandValidator()
    {
        RuleFor(x => x.UserId)
            .NotEqual(Guid.Empty)
            .WithMessage("Geçerli kullanıcı veya public token gerekli.");

        RuleFor(x => x.ReplySuggestion)
            .NotEmpty()
            .WithMessage("replySuggestion zorunludur.");
    }
}
