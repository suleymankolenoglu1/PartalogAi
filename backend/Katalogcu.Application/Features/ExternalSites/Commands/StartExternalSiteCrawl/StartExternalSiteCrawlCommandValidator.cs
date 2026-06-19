using FluentValidation;

namespace Katalogcu.Application.Features.ExternalSites.Commands.StartExternalSiteCrawl;

public sealed class StartExternalSiteCrawlCommandValidator : AbstractValidator<StartExternalSiteCrawlCommand>
{
    public StartExternalSiteCrawlCommandValidator()
    {
        RuleFor(x => x.SiteId).NotEmpty();
    }
}
