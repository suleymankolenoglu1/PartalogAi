using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.ExternalSites.Common;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Commands.CreateExternalSite;

public sealed record CreateExternalSiteCommand(
    string Name,
    string BaseUrl,
    string PreferredCrawlMode) : IRequest<OperationResult<ExternalSiteDto>>;
