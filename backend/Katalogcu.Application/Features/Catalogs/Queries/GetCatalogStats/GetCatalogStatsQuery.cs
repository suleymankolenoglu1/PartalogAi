using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Catalogs.Common;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetCatalogStats;

public sealed record GetCatalogStatsQuery(Guid UserId) : IRequest<OperationResult<CatalogStatsDto>>;
