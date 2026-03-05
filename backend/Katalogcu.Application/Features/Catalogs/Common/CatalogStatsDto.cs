using Katalogcu.Application.Common.Models;

namespace Katalogcu.Application.Features.Catalogs.Common;

public sealed class CatalogStatsDto
{
    public int TotalCatalogs { get; init; }
    public int TotalParts { get; init; }
    public int TotalViews { get; init; }
    public int ViewsLast7Days { get; init; }
    public int UniqueViewersLast30Days { get; init; }
    public int StorefrontVisitsTotal { get; init; }
    public int StorefrontVisitsToday { get; init; }
    public int StorefrontVisitsLast7Days { get; init; }
    public int StorefrontUniqueVisitorsLast30Days { get; init; }
    public int PendingCount { get; init; }
    public IReadOnlyList<CatalogRecentSummary> RecentCatalogs { get; init; } = [];
    public IReadOnlyList<CatalogTopViewedSummary> TopViewedCatalogs { get; init; } = [];
    public int VisualEmbeddingCount { get; init; }
}
