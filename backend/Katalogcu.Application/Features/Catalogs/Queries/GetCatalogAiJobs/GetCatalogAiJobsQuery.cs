using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Catalogs.Common;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetCatalogAiJobs;

public sealed record GetCatalogAiJobsQuery(Guid UserId, int Take = 50)
    : IRequest<OperationResult<CatalogAiJobsDto>>;
