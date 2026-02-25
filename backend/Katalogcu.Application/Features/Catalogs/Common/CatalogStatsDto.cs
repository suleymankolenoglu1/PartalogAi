using Katalogcu.Application.Common.Models;

namespace Katalogcu.Application.Features.Catalogs.Common;

public sealed class CatalogStatsDto
{
    public int TotalCatalogs { get; init; }
    public int TotalParts { get; init; }
    public int TotalViews { get; init; }
    public int PendingCount { get; init; }
    public IReadOnlyList<CatalogRecentSummary> RecentCatalogs { get; init; } = [];
    public int VisualEmbeddingCount { get; init; }
}
