using FluentValidation;

namespace Katalogcu.Application.Features.Ai.Commands.RunExpertChat;

public sealed class RunExpertChatCommandValidator : AbstractValidator<RunExpertChatCommand>
{
    public RunExpertChatCommandValidator()
    {
    }
}
