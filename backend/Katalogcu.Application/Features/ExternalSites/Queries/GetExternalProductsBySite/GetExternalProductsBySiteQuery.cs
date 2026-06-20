using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.ExternalSites.Common;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Queries.GetExternalProductsBySite;

public sealed record GetExternalProductsBySiteQuery(Guid SiteId, int Page = 1, int PageSize = 50)
    : IRequest<OperationResult<ExternalProductsBySiteResponse>>;
