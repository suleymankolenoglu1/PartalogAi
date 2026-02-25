using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.Infrastructure.Repositories;

public sealed class CatalogProcessingRepository : ICatalogProcessingRepository
{
    private readonly AppDbContext _context;

    public CatalogProcessingRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Catalog?> GetCatalogForProcessingAsync(Guid catalogId, CancellationToken cancellationToken)
    {
        return _context.Catalogs.FirstOrDefaultAsync(c => c.Id == catalogId, cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogPage>> GetCatalogPagesForProcessingAsync(Guid catalogId, CancellationToken cancellationToken)
    {
        return await _context.CatalogPages
            .Where(p => p.CatalogId == catalogId)
            .OrderBy(p => p.PageNumber)
            .ToListAsync(cancellationToken);
    }

    public Task DeleteCatalogItemsByCatalogAndPageNumberAsync(Guid catalogId, string pageNumber, CancellationToken cancellationToken)
    {
        return _context.CatalogItems
            .Where(ci => ci.CatalogId == catalogId && ci.PageNumber == pageNumber)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task AddCatalogItemsAsync(IEnumerable<CatalogItem> items, CancellationToken cancellationToken)
    {
        return _context.CatalogItems.AddRangeAsync(items, cancellationToken);
    }

    public Task DeleteHotspotsByPageIdAsync(Guid pageId, CancellationToken cancellationToken)
    {
        return _context.Hotspots.Where(h => h.PageId == pageId).ExecuteDeleteAsync(cancellationToken);
    }

    public Task AddHotspotsAsync(IEnumerable<Hotspot> hotspots, CancellationToken cancellationToken)
    {
        return _context.Hotspots.AddRangeAsync(hotspots, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
