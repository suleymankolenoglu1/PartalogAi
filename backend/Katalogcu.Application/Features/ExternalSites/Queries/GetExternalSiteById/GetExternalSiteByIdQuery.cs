using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.ExternalSites.Common;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Queries.GetExternalSiteById;

public sealed record GetExternalSiteByIdQuery(Guid SiteId) : IRequest<OperationResult<ExternalSiteDto>>;
