using FluentValidation;

namespace Katalogcu.Application.Features.ExternalLinks.Commands.MarkApprovedExternalMatchBroken;

public sealed class MarkApprovedExternalMatchBrokenCommandValidator : AbstractValidator<MarkApprovedExternalMatchBrokenCommand>
{
    public MarkApprovedExternalMatchBrokenCommandValidator()
    {
        RuleFor(x => x.MatchId).NotEmpty();
    }
}
