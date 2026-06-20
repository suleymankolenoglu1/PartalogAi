using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface ICatalogExternalMatchRepository
{
    Task<Catalog?> GetOwnedCatalogWithPagesAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogItem>> GetCatalogItemsByCatalogIdAsync(Guid catalogId, Guid userId, int skip, int take, CancellationToken cancellationToken);
    Task<CatalogItem?> GetCatalogItemByIdWithCatalogAsync(Guid catalogItemId, Guid userId, CancellationToken cancellationToken);
    Task<ExternalSite?> GetExternalSiteByIdAsync(Guid externalSiteId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExternalProduct>> GetActiveExternalProductsBySiteIdAsync(Guid externalSiteId, Guid userId, int skip, int take, CancellationToken cancellationToken);
    Task<ExternalProduct?> GetExternalProductByIdAsync(Guid externalProductId, Guid externalSiteId, Guid userId, CancellationToken cancellationToken);
    Task<ExternalProduct?> GetExternalProductBySourceUrlAsync(Guid externalSiteId, string sourceUrl, Guid userId, CancellationToken cancellationToken);
    Task<CatalogItemExternalMatch?> GetMatchByIdAsync(Guid matchId, Guid userId, CancellationToken cancellationToken);
    Task<CatalogItemExternalMatch?> GetMatchByIdForLinkRefreshAsync(Guid matchId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogItemExternalMatch>> GetMatchesByCatalogItemIdAsync(Guid catalogItemId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogItemExternalMatch>> GetMatchesByCatalogIdAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogItemExternalMatch>> GetNeedsReviewMatchesByCatalogIdAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<CatalogItemExternalMatch> Items, int TotalCount)> GetAutoMatchedMatchesByCatalogIdAsync(Guid catalogId, Guid userId, int page, int pageSize, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogItemExternalMatch>> GetApprovedMatchesByCatalogIdAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogItemExternalMatch>> GetPublishedMatchesByCatalogIdAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken);
    Task<CatalogItemExternalMatch?> GetPublishedMatchByCatalogItemIdAsync(Guid catalogItemId, Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogItemExternalMatch>> GetMatchesNeedingLinkRecheckAsync(DateTime staleBeforeUtc, CancellationToken cancellationToken);
    Task AddMatchesAsync(IEnumerable<CatalogItemExternalMatch> matches, CancellationToken cancellationToken);
    Task AddExternalProductAsync(ExternalProduct product, CancellationToken cancellationToken);
    Task AddLinkCheckAsync(ExternalProductLinkCheck linkCheck, CancellationToken cancellationToken);
    void RemoveMatches(IEnumerable<CatalogItemExternalMatch> matches);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
