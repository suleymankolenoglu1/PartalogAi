using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface ICatalogRepository
{
    Task<IReadOnlyList<Catalog>> GetPublicCatalogsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<Catalog>> GetPublicCatalogsByUserAsync(
        Guid userId,
        IReadOnlyCollection<Guid>? allowedCatalogIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetPublishedCatalogIdsByUserAsync(
        Guid userId,
        IReadOnlyCollection<Guid> requestedCatalogIds,
        CancellationToken cancellationToken);

    Task<Catalog?> GetOwnedCatalogAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken);

    Task<bool> FolderExistsForUserAsync(Guid folderId, Guid userId, CancellationToken cancellationToken);

    Task<int> CountCatalogsByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<int> CountProductsByCatalogOwnerAsync(Guid userId, CancellationToken cancellationToken);

    Task<int> CountPendingCatalogsByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogRecentSummary>> GetRecentCatalogsByUserAsync(Guid userId, int take, CancellationToken cancellationToken);

    Task<int> CountVisualEmbeddingCatalogItemsByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Catalog>> GetCatalogsByUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<Catalog?> GetCatalogByIdForAccessAsync(
        Guid catalogId,
        Guid userId,
        bool publicOnlyPublished,
        IReadOnlyCollection<Guid>? allowedCatalogIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogItem>> GetCatalogItemsForPageAsync(
        Guid catalogId,
        string pageNumber,
        Guid userId,
        bool publicOnlyPublished,
        IReadOnlyCollection<Guid>? allowedCatalogIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, Product>> GetOwnedStockedProductsByCodesAsync(
        Guid userId,
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken);

    Task AddCatalogAsync(Catalog catalog, CancellationToken cancellationToken);

    Task AddCatalogPagesAsync(IEnumerable<CatalogPage> pages, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetProductIdsByCatalogIdAsync(Guid catalogId, CancellationToken cancellationToken);

    Task DeleteOrderItemsByProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    Task DeleteHotspotsByProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);

    Task DeleteProductsByCatalogIdAsync(Guid catalogId, CancellationToken cancellationToken);

    Task DeleteCatalogItemsByCatalogIdAsync(Guid catalogId, CancellationToken cancellationToken);

    Task DeleteCatalogPagesByCatalogIdAsync(Guid catalogId, CancellationToken cancellationToken);

    void RemoveCatalog(Catalog catalog);

    Task<CatalogPage?> GetCatalogPageByIdAsync(Guid pageId, CancellationToken cancellationToken);

    Task DeleteHotspotsByPageIdAsync(Guid pageId, CancellationToken cancellationToken);

    Task DeleteCatalogItemsByCatalogAndPageNumberAsync(Guid catalogId, string pageNumber, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
