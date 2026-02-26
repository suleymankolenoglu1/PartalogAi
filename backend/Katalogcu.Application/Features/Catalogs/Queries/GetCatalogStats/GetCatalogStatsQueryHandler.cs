using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Application.Features.Catalogs.Common;
using MediatR;

namespace Katalogcu.Application.Features.Catalogs.Queries.GetCatalogStats;

public sealed class GetCatalogStatsQueryHandler : IRequestHandler<GetCatalogStatsQuery, OperationResult<CatalogStatsDto>>
{
    private readonly ICatalogRepository _catalogRepository;

    public GetCatalogStatsQueryHandler(ICatalogRepository catalogRepository)
    {
        _catalogRepository = catalogRepository;
    }

    public async Task<OperationResult<CatalogStatsDto>> Handle(GetCatalogStatsQuery request, CancellationToken cancellationToken)
    {
        var totalCatalogs = await _catalogRepository.CountCatalogsByUserAsync(request.UserId, cancellationToken);
        var totalParts = await _catalogRepository.CountProductsByCatalogOwnerAsync(request.UserId, cancellationToken);
        var pendingCount = await _catalogRepository.CountPendingCatalogsByUserAsync(request.UserId, cancellationToken);
        var recentCatalogs = await _catalogRepository.GetRecentCatalogsByUserAsync(request.UserId, 5, cancellationToken);
        var topViewedCatalogs = await _catalogRepository.GetTopViewedCatalogsByUserAsync(request.UserId, 5, cancellationToken);
        var visualEmbeddingCount = await _catalogRepository.CountVisualEmbeddingCatalogItemsByUserAsync(request.UserId, cancellationToken);
        var totalViews = await _catalogRepository.CountCatalogViewsByUserAsync(request.UserId, cancellationToken);
        var now = DateTime.UtcNow;
        var viewsLast7Days = await _catalogRepository.CountCatalogViewsByUserInRangeAsync(
            request.UserId,
            now.AddDays(-7),
            cancellationToken);
        var uniqueViewersLast30Days = await _catalogRepository.CountUniqueCatalogViewersByUserInRangeAsync(
            request.UserId,
            now.AddDays(-30),
            cancellationToken);

        return OperationResult<CatalogStatsDto>.Success(new CatalogStatsDto
        {
            TotalCatalogs = totalCatalogs,
            TotalParts = totalParts,
            TotalViews = totalViews,
            ViewsLast7Days = viewsLast7Days,
            UniqueViewersLast30Days = uniqueViewersLast30Days,
            PendingCount = pendingCount,
            RecentCatalogs = recentCatalogs,
            TopViewedCatalogs = topViewedCatalogs,
            VisualEmbeddingCount = visualEmbeddingCount
        });
    }
}
