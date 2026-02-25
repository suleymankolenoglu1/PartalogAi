using FluentValidation;

namespace Katalogcu.Application.Features.Chat.Queries.ResolveChatUser;

public sealed class ResolveChatUserQueryValidator : AbstractValidator<ResolveChatUserQuery>
{
    public ResolveChatUserQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => x.AuthenticatedUserId != Guid.Empty || !string.IsNullOrWhiteSpace(x.PublicToken))
            .WithMessage("Geçerli kullanıcı veya public token gerekli.");
    }
}
