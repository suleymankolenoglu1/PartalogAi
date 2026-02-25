using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Application.Common.Models;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.Infrastructure.Repositories;

public sealed class CatalogRepository : ICatalogRepository
{
    private readonly AppDbContext _context;

    public CatalogRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Catalog>> GetPublicCatalogsAsync(CancellationToken cancellationToken)
    {
        return await _context.Catalogs
            .AsNoTracking()
            .Where(c => c.Status == "Published")
            .Include(c => c.Pages.OrderBy(p => p.PageNumber).Take(1))
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Catalog>> GetPublicCatalogsByUserAsync(
        Guid userId,
        IReadOnlyCollection<Guid>? allowedCatalogIds,
        CancellationToken cancellationToken)
    {
        var query = _context.Catalogs
            .AsNoTracking()
            .Where(c => c.Status == "Published" && c.UserId == userId);

        if (allowedCatalogIds is { Count: > 0 })
        {
            query = query.Where(c => allowedCatalogIds.Contains(c.Id));
        }

        return await query
            .Include(c => c.Pages.OrderBy(p => p.PageNumber).Take(1))
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetPublishedCatalogIdsByUserAsync(
        Guid userId,
        IReadOnlyCollection<Guid> requestedCatalogIds,
        CancellationToken cancellationToken)
    {
        return await _context.Catalogs
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.Status == "Published" && requestedCatalogIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<Catalog?> GetOwnedCatalogAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.Catalogs.FirstOrDefaultAsync(c => c.Id == catalogId && c.UserId == userId, cancellationToken);
    }

    public Task<bool> FolderExistsForUserAsync(Guid folderId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.Folders.AnyAsync(f => f.Id == folderId && f.UserId == userId, cancellationToken);
    }

    public Task<int> CountCatalogsByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _context.Catalogs.CountAsync(c => c.UserId == userId, cancellationToken);
    }

    public Task<int> CountProductsByCatalogOwnerAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _context.Products.Include(p => p.Catalog).CountAsync(p => p.Catalog != null && p.Catalog.UserId == userId, cancellationToken);
    }

    public Task<int> CountPendingCatalogsByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _context.Catalogs
            .Where(c => c.UserId == userId)
            .CountAsync(c => c.Status == "Processing" || c.Status == "Pending" || c.Status == "Uploading", cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogRecentSummary>> GetRecentCatalogsByUserAsync(Guid userId, int take, CancellationToken cancellationToken)
    {
        return await _context.Catalogs
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedDate)
            .Take(take)
            .Select(c => new CatalogRecentSummary
            {
                Id = c.Id,
                Name = c.Name,
                Status = c.Status,
                PartCount = _context.Products.Count(p => p.CatalogId == c.Id),
                CreatedDate = c.CreatedDate
            })
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountVisualEmbeddingCatalogItemsByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return _context.CatalogItems.CountAsync(ci => ci.Catalog.UserId == userId && ci.VisualEmbedding != null, cancellationToken);
    }

    public async Task<IReadOnlyList<Catalog>> GetCatalogsByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _context.Catalogs
            .Where(c => c.UserId == userId)
            .Include(c => c.Pages)
            .OrderByDescending(c => c.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public Task<Catalog?> GetCatalogByIdForAccessAsync(
        Guid catalogId,
        Guid userId,
        bool publicOnlyPublished,
        IReadOnlyCollection<Guid>? allowedCatalogIds,
        CancellationToken cancellationToken)
    {
        var query = _context.Catalogs
            .Include(c => c.Pages.OrderBy(p => p.PageNumber))
            .ThenInclude(p => p.Hotspots)
            .Where(c => c.Id == catalogId && c.UserId == userId);

        if (publicOnlyPublished)
        {
            query = query.Where(c => c.Status == "Published");
            if (allowedCatalogIds is { Count: > 0 })
            {
                query = query.Where(c => allowedCatalogIds.Contains(c.Id));
            }
        }

        return query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogItem>> GetCatalogItemsForPageAsync(
        Guid catalogId,
        string pageNumber,
        Guid userId,
        bool publicOnlyPublished,
        IReadOnlyCollection<Guid>? allowedCatalogIds,
        CancellationToken cancellationToken)
    {
        var query = _context.CatalogItems
            .Include(ci => ci.Catalog)
            .AsNoTracking()
            .Where(ci => ci.CatalogId == catalogId && ci.PageNumber == pageNumber && ci.Catalog.UserId == userId);

        if (publicOnlyPublished)
        {
            query = query.Where(ci => ci.Catalog.Status == "Published");
            if (allowedCatalogIds is { Count: > 0 })
            {
                query = query.Where(ci => allowedCatalogIds.Contains(ci.CatalogId));
            }
        }

        return await query.OrderBy(ci => ci.RefNumber).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, Product>> GetOwnedStockedProductsByCodesAsync(
        Guid userId,
        IReadOnlyCollection<string> codes,
        CancellationToken cancellationToken)
    {
        if (codes.Count == 0)
        {
            return new Dictionary<string, Product>();
        }

        return await _context.Products
            .Include(p => p.Catalog)
            .AsNoTracking()
            .Where(p => codes.Contains(p.Code) && p.Catalog != null && p.Catalog.UserId == userId)
            .GroupBy(p => p.Code)
            .Select(g => g.First())
            .ToDictionaryAsync(p => p.Code, cancellationToken);
    }

    public Task AddCatalogAsync(Catalog catalog, CancellationToken cancellationToken)
    {
        return _context.Catalogs.AddAsync(catalog, cancellationToken).AsTask();
    }

    public Task AddCatalogPagesAsync(IEnumerable<CatalogPage> pages, CancellationToken cancellationToken)
    {
        return _context.CatalogPages.AddRangeAsync(pages, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetProductIdsByCatalogIdAsync(Guid catalogId, CancellationToken cancellationToken)
    {
        return await _context.Products
            .Where(p => p.CatalogId == catalogId)
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);
    }

    public Task DeleteOrderItemsByProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0) return Task.CompletedTask;
        return _context.OrderItems.Where(oi => productIds.Contains(oi.ProductId)).ExecuteDeleteAsync(cancellationToken);
    }

    public Task DeleteHotspotsByProductIdsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken)
    {
        if (productIds.Count == 0) return Task.CompletedTask;
        return _context.Hotspots.Where(h => h.ProductId.HasValue && productIds.Contains(h.ProductId.Value)).ExecuteDeleteAsync(cancellationToken);
    }

    public Task DeleteProductsByCatalogIdAsync(Guid catalogId, CancellationToken cancellationToken)
    {
        return _context.Products.Where(p => p.CatalogId == catalogId).ExecuteDeleteAsync(cancellationToken);
    }

    public Task DeleteCatalogItemsByCatalogIdAsync(Guid catalogId, CancellationToken cancellationToken)
    {
        return _context.CatalogItems.Where(ci => ci.CatalogId == catalogId).ExecuteDeleteAsync(cancellationToken);
    }

    public Task DeleteCatalogPagesByCatalogIdAsync(Guid catalogId, CancellationToken cancellationToken)
    {
        return _context.CatalogPages.Where(cp => cp.CatalogId == catalogId).ExecuteDeleteAsync(cancellationToken);
    }

    public void RemoveCatalog(Catalog catalog)
    {
        _context.Catalogs.Remove(catalog);
    }

    public Task<CatalogPage?> GetCatalogPageByIdAsync(Guid pageId, CancellationToken cancellationToken)
    {
        return _context.CatalogPages.FirstOrDefaultAsync(p => p.Id == pageId, cancellationToken);
    }

    public Task DeleteHotspotsByPageIdAsync(Guid pageId, CancellationToken cancellationToken)
    {
        return _context.Hotspots.Where(h => h.PageId == pageId).ExecuteDeleteAsync(cancellationToken);
    }

    public Task DeleteCatalogItemsByCatalogAndPageNumberAsync(Guid catalogId, string pageNumber, CancellationToken cancellationToken)
    {
        return _context.CatalogItems
            .Where(ci => ci.CatalogId == catalogId && ci.PageNumber == pageNumber)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
