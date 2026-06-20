using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.Infrastructure.Repositories;

public sealed class CatalogExternalMatchRepository : ICatalogExternalMatchRepository
{
    private readonly AppDbContext _context;

    public CatalogExternalMatchRepository(AppDbContext context)
    {
        _context = context;
    }

    public Task<Catalog?> GetOwnedCatalogWithPagesAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.Catalogs
            .Include(x => x.Pages)
            .FirstOrDefaultAsync(x => x.Id == catalogId && x.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogItem>> GetCatalogItemsByCatalogIdAsync(
        Guid catalogId,
        Guid userId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return await _context.CatalogItems
            .AsNoTracking()
            .Where(x => x.CatalogId == catalogId && x.Catalog.UserId == userId)
            .OrderBy(x => x.CreatedDate)
            .ThenBy(x => x.Id)
            .Skip(Math.Max(0, skip))
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);
    }

    public Task<CatalogItem?> GetCatalogItemByIdWithCatalogAsync(Guid catalogItemId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.CatalogItems
            .Include(x => x.Catalog)
            .FirstOrDefaultAsync(x => x.Id == catalogItemId && x.Catalog.UserId == userId, cancellationToken);
    }

    public Task<ExternalSite?> GetExternalSiteByIdAsync(Guid externalSiteId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.ExternalSites.FirstOrDefaultAsync(x => x.Id == externalSiteId && x.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<ExternalProduct>> GetActiveExternalProductsBySiteIdAsync(
        Guid externalSiteId,
        Guid userId,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        return await _context.ExternalProducts
            .AsNoTracking()
            .Include(x => x.OemNumbers)
            .Where(x => x.ExternalSiteId == externalSiteId && x.ExternalSite != null && x.ExternalSite.UserId == userId && x.IsActive)
            .OrderBy(x => x.CreatedDate)
            .ThenBy(x => x.Id)
            .Skip(Math.Max(0, skip))
            .Take(Math.Max(1, take))
            .ToListAsync(cancellationToken);
    }

    public Task<ExternalProduct?> GetExternalProductByIdAsync(Guid externalProductId, Guid externalSiteId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.ExternalProducts
            .Include(x => x.ExternalSite)
            .Include(x => x.OemNumbers)
            .FirstOrDefaultAsync(
                x => x.Id == externalProductId &&
                     x.ExternalSiteId == externalSiteId &&
                     x.ExternalSite != null &&
                     x.ExternalSite.UserId == userId,
                cancellationToken);
    }

    public Task<ExternalProduct?> GetExternalProductBySourceUrlAsync(Guid externalSiteId, string sourceUrl, Guid userId, CancellationToken cancellationToken)
    {
        return _context.ExternalProducts
            .Include(x => x.ExternalSite)
            .Include(x => x.OemNumbers)
            .FirstOrDefaultAsync(
                x => x.ExternalSiteId == externalSiteId &&
                     x.SourceUrl == sourceUrl &&
                     x.ExternalSite != null &&
                     x.ExternalSite.UserId == userId,
                cancellationToken);
    }

    public Task<CatalogItemExternalMatch?> GetMatchByIdAsync(Guid matchId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.CatalogItemExternalMatches
            .Include(x => x.Catalog)
            .FirstOrDefaultAsync(x => x.Id == matchId && x.Catalog != null && x.Catalog.UserId == userId, cancellationToken);
    }

    public Task<CatalogItemExternalMatch?> GetMatchByIdForLinkRefreshAsync(Guid matchId, CancellationToken cancellationToken)
    {
        return _context.CatalogItemExternalMatches
            .Include(x => x.ExternalProduct)
            .FirstOrDefaultAsync(x => x.Id == matchId, cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogItemExternalMatch>> GetMatchesByCatalogItemIdAsync(Guid catalogItemId, Guid userId, CancellationToken cancellationToken)
    {
        return await _context.CatalogItemExternalMatches
            .Where(x =>
                x.CatalogItemId == catalogItemId &&
                x.Catalog != null &&
                x.Catalog.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogItemExternalMatch>> GetMatchesByCatalogIdAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken)
    {
        return await _context.CatalogItemExternalMatches
            .Where(x =>
                x.CatalogId == catalogId &&
                x.Catalog != null &&
                x.Catalog.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogItemExternalMatch>> GetNeedsReviewMatchesByCatalogIdAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken)
    {
        return await _context.CatalogItemExternalMatches
            .AsNoTracking()
            .Where(x =>
                x.CatalogId == catalogId &&
                x.Catalog != null &&
                x.Catalog.UserId == userId &&
                x.Status == "needs_review")
            .OrderByDescending(x => x.ConfidenceScore)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<CatalogItemExternalMatch> Items, int TotalCount)> GetAutoMatchedMatchesByCatalogIdAsync(
        Guid catalogId,
        Guid userId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var query = _context.CatalogItemExternalMatches
            .AsNoTracking()
            .Where(x =>
                x.CatalogId == catalogId &&
                x.Catalog != null &&
                x.Catalog.UserId == userId &&
                x.Status == "auto_matched");

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(x => x.MatchedAtUtc)
            .ThenByDescending(x => x.ConfidenceScore)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<IReadOnlyList<CatalogItemExternalMatch>> GetApprovedMatchesByCatalogIdAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken)
    {
        return await _context.CatalogItemExternalMatches
            .AsNoTracking()
            .Where(x =>
                x.CatalogId == catalogId &&
                x.Catalog != null &&
                x.Catalog.UserId == userId &&
                x.Status == "approved")
            .OrderBy(x => x.CatalogItemId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogItemExternalMatch>> GetPublishedMatchesByCatalogIdAsync(Guid catalogId, Guid userId, CancellationToken cancellationToken)
    {
        return await _context.CatalogItemExternalMatches
            .AsNoTracking()
            .Where(x =>
                x.CatalogId == catalogId &&
                x.Catalog != null &&
                x.Catalog.UserId == userId &&
                x.IsActive &&
                x.Status == "approved" &&
                (x.IsLinkHealthy == null || x.IsLinkHealthy == true))
            .OrderBy(x => x.CatalogItemId)
            .ToListAsync(cancellationToken);
    }

    public Task<CatalogItemExternalMatch?> GetPublishedMatchByCatalogItemIdAsync(Guid catalogItemId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.CatalogItemExternalMatches
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.CatalogItemId == catalogItemId &&
                     x.Catalog != null &&
                     x.Catalog.UserId == userId &&
                     x.IsActive &&
                     x.Status == "approved" &&
                     (x.IsLinkHealthy == null || x.IsLinkHealthy == true),
                cancellationToken);
    }

    public async Task<IReadOnlyList<CatalogItemExternalMatch>> GetMatchesNeedingLinkRecheckAsync(DateTime staleBeforeUtc, CancellationToken cancellationToken)
    {
        return await _context.CatalogItemExternalMatches
            .Include(x => x.ExternalProduct)
            .Where(x =>
                x.IsActive &&
                x.ExternalProductId != null &&
                ((x.Status == "broken_link") ||
                 (x.Status == "approved" && (x.LastLinkCheckAtUtc == null || x.LastLinkCheckAtUtc < staleBeforeUtc))))
            .ToListAsync(cancellationToken);
    }

    public Task AddMatchesAsync(IEnumerable<CatalogItemExternalMatch> matches, CancellationToken cancellationToken)
    {
        return _context.CatalogItemExternalMatches.AddRangeAsync(matches, cancellationToken);
    }

    public Task AddExternalProductAsync(ExternalProduct product, CancellationToken cancellationToken)
    {
        return _context.ExternalProducts.AddAsync(product, cancellationToken).AsTask();
    }

    public Task AddLinkCheckAsync(ExternalProductLinkCheck linkCheck, CancellationToken cancellationToken)
    {
        return _context.ExternalProductLinkChecks.AddAsync(linkCheck, cancellationToken).AsTask();
    }

    public void RemoveMatches(IEnumerable<CatalogItemExternalMatch> matches)
    {
        _context.CatalogItemExternalMatches.RemoveRange(matches);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
