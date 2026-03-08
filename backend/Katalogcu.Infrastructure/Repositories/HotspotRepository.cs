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

    public Task<CatalogPage?> GetCatalogPageByIdForUserAsync(Guid pageId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.CatalogPages
            .Include(p => p.Catalog)
            .FirstOrDefaultAsync(
                p => p.Id == pageId &&
                     p.Catalog != null &&
                     p.Catalog.UserId == userId,
                cancellationToken);
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

    public Task<Hotspot?> GetHotspotByIdForUserAsync(Guid hotspotId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.Hotspots
            .Include(h => h.Page)
            .ThenInclude(p => p!.Catalog)
            .FirstOrDefaultAsync(
                h => h.Id == hotspotId &&
                     h.Page != null &&
                     h.Page.Catalog != null &&
                     h.Page.Catalog.UserId == userId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<Hotspot>> GetHotspotsByPageIdAsync(Guid pageId, CancellationToken cancellationToken)
    {
        return await _context.Hotspots
            .Where(h => h.PageId == pageId)
            .ToListAsync(cancellationToken);
    }

    public void RemoveHotspot(Hotspot hotspot)
    {
        _context.Hotspots.Remove(hotspot);
    }

    public void RemoveHotspots(IEnumerable<Hotspot> hotspots)
    {
        _context.Hotspots.RemoveRange(hotspots);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
