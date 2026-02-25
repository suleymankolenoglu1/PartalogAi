using FluentValidation;

namespace Katalogcu.Application.Features.Chat.Commands.AskChat;

public sealed class AskChatCommandValidator : AbstractValidator<AskChatCommand>
{
    public AskChatCommandValidator()
    {
        RuleFor(x => x.CatalogIds)
            .NotNull()
            .Must(x => x.Count > 0)
            .WithMessage("Katalog bilgisi bulunamadı.");
    }
}
