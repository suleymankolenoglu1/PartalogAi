using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface IHotspotRepository
{
    Task<CatalogPage?> GetCatalogPageByIdAsync(Guid pageId, CancellationToken cancellationToken);

    Task AddHotspotsAsync(IEnumerable<Hotspot> hotspots, CancellationToken cancellationToken);

    Task AddHotspotAsync(Hotspot hotspot, CancellationToken cancellationToken);

    Task<Hotspot?> GetHotspotByIdAsync(Guid hotspotId, CancellationToken cancellationToken);

    void RemoveHotspot(Hotspot hotspot);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
