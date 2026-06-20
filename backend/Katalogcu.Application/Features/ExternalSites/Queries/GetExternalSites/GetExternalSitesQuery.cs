using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.ExternalSites.Common;
using MediatR;

namespace Katalogcu.Application.Features.ExternalSites.Queries.GetExternalSites;

public sealed record GetExternalSitesQuery : IRequest<OperationResult<IReadOnlyList<ExternalSiteDto>>>;
