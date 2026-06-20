using FluentValidation;

namespace Katalogcu.Application.Features.ExternalSites.Commands.MarkMissingExternalProductsInactive;

public sealed class MarkMissingExternalProductsInactiveCommandValidator : AbstractValidator<MarkMissingExternalProductsInactiveCommand>
{
    public MarkMissingExternalProductsInactiveCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
        RuleForEach(x => x.SeenSourceUrls).NotEmpty();
    }
}
