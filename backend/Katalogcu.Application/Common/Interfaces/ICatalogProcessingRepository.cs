using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface ICatalogProcessingRepository
{
    Task<Catalog?> GetCatalogForProcessingAsync(Guid catalogId, CancellationToken cancellationToken);

    Task<IReadOnlyList<CatalogPage>> GetCatalogPagesForProcessingAsync(Guid catalogId, CancellationToken cancellationToken);

    Task DeleteCatalogItemsByCatalogAndPageNumberAsync(Guid catalogId, string pageNumber, CancellationToken cancellationToken);

    Task AddCatalogItemsAsync(IEnumerable<CatalogItem> items, CancellationToken cancellationToken);

    Task DeleteHotspotsByPageIdAsync(Guid pageId, CancellationToken cancellationToken);

    Task AddHotspotsAsync(IEnumerable<Hotspot> hotspots, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
