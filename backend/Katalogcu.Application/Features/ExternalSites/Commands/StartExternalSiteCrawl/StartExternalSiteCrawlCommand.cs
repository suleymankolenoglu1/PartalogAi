using Katalogcu.Application.Common.Models;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Commands.StartExternalSiteCrawl;

public sealed record StartExternalSiteCrawlCommand(Guid SiteId) : IRequest<OperationResult<StartExternalSiteCrawlResponse>>;

public sealed class StartExternalSiteCrawlResponse
{
    public Guid SiteId { get; init; }
    public Guid CrawlId { get; init; }
    public string Status { get; init; } = string.Empty;
}
