using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.Infrastructure.Repositories;

public sealed class HotspotRepository : IHotspotRepository
{
    private readonly AppDbContext _context;

    public HotspotRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<CatalogPage?> GetCatalogPageByIdAsync(Guid pageId, CancellationToken cancellationToken)
    {
        return _context.CatalogPages.FirstOrDefaultAsync(p => p.Id == pageId, cancellationToken);
    }

    public Task AddHotspotsAsync(IEnumerable<Hotspot> hotspots, CancellationToken cancellationToken)
    {
        return _context.Hotspots.AddRangeAsync(hotspots, cancellationToken);
    }

    public Task AddHotspotAsync(Hotspot hotspot, CancellationToken cancellationToken)
    {
        return _context.Hotspots.AddAsync(hotspot, cancellationToken).AsTask();
    }

    public Task<Hotspot?> GetHotspotByIdAsync(Guid hotspotId, CancellationToken cancellationToken)
    {
        return _context.Hotspots.FirstOrDefaultAsync(h => h.Id == hotspotId, cancellationToken);
    }

    public void RemoveHotspot(Hotspot hotspot)
    {
        _context.Hotspots.Remove(hotspot);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
