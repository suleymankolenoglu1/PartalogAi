using FluentValidation;

namespace Katalogcu.Application.Features.ExternalSites.Commands.DeleteExternalSite;

public sealed class DeleteExternalSiteCommandValidator : AbstractValidator<DeleteExternalSiteCommand>
{
    public DeleteExternalSiteCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
    }
}
