using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Catalogs.Common;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetCatalogAiJobs;

public sealed class GetCatalogAiJobsQueryHandler : IRequestHandler<GetCatalogAiJobsQuery, OperationResult<CatalogAiJobsDto>>
{
    private readonly ICatalogAiJobRepository _catalogAiJobRepository;

    public GetCatalogAiJobsQueryHandler(ICatalogAiJobRepository catalogAiJobRepository)
    {
        _catalogAiJobRepository = catalogAiJobRepository;
    }

    public async Task<OperationResult<CatalogAiJobsDto>> Handle(GetCatalogAiJobsQuery request, CancellationToken cancellationToken)
    {
        var jobs = await _catalogAiJobRepository.GetJobsByUserAsync(request.UserId, request.Take, cancellationToken);
        var summary = await _catalogAiJobRepository.GetJobSummaryByUserAsync(request.UserId, cancellationToken);

        return OperationResult<CatalogAiJobsDto>.Success(new CatalogAiJobsDto
        {
            Summary = summary,
            Jobs = jobs
        });
    }
}
