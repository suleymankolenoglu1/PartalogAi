using FluentValidation;

namespace Katalogcu.Application.Features.Chat.Queries.ResolveChatCatalogAccess;

public sealed class ResolveChatCatalogAccessQueryValidator : AbstractValidator<ResolveChatCatalogAccessQuery>
{
    public ResolveChatCatalogAccessQueryValidator()
    {
        RuleFor(x => x.RequestedCatalogIds)
            .NotNull();
    }
}
