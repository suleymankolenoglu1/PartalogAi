using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.ExternalSites.Common;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Commands.UpdateExternalSite;

public sealed record UpdateExternalSiteCommand(
    Guid SiteId,
    string Name,
    string BaseUrl,
    string PreferredCrawlMode,
    string Status) : IRequest<OperationResult<ExternalSiteDto>>;
