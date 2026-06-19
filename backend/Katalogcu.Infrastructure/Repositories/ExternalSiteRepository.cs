using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Domain.Entities;
using Katalogcu.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Katalogcu.Infrastructure.Repositories;

public sealed class ExternalSiteRepository : IExternalSiteRepository
{
    private readonly AppDbContext _context;

    public ExternalSiteRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ExternalSite>> GetSitesByUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _context.ExternalSites
            .AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<Guid, ExternalSiteCrawl>> GetLatestCrawlsBySiteIdsAsync(IReadOnlyCollection<Guid> siteIds, CancellationToken cancellationToken)
    {
        if (siteIds.Count == 0)
        {
            return [];
        }

        return await _context.ExternalSiteCrawls
            .AsNoTracking()
            .Where(x => siteIds.Contains(x.ExternalSiteId))
            .GroupBy(x => x.ExternalSiteId)
            .Select(g => g.OrderByDescending(x => x.CreatedDate).First())
            .ToDictionaryAsync(x => x.ExternalSiteId, x => x, cancellationToken);
    }

    public Task<ExternalSite?> GetSiteByIdAsync(Guid siteId, Guid userId, CancellationToken cancellationToken)
    {
        return _context.ExternalSites.FirstOrDefaultAsync(x => x.Id == siteId && x.UserId == userId, cancellationToken);
    }

    public Task<ExternalSite?> GetSiteByIdAsync(Guid siteId, CancellationToken cancellationToken)
    {
        return _context.ExternalSites.FirstOrDefaultAsync(x => x.Id == siteId, cancellationToken);
    }

    public async Task<(IReadOnlyList<ExternalProduct> Products, int TotalCount)> GetProductsBySiteAsync(Guid siteId, Guid userId, int skip, int take, CancellationToken cancellationToken)
    {
        var query = _context.ExternalProducts
            .AsNoTracking()
            .Include(x => x.OemNumbers)
            .Where(x => x.ExternalSiteId == siteId && x.ExternalSite != null && x.ExternalSite.UserId == userId)
            .OrderByDescending(x => x.LastSeenAtUtc ?? x.CreatedDate)
            .ThenBy(x => x.Title);

        var totalCount = await query.CountAsync(cancellationToken);
        var products = await query
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (products, totalCount);
    }

    public Task<bool> BaseUrlExistsAsync(Guid userId, string baseUrl, Guid? excludeSiteId, CancellationToken cancellationToken)
    {
        return _context.ExternalSites.AnyAsync(
            x => x.UserId == userId
                 && x.BaseUrl == baseUrl
                 && (!excludeSiteId.HasValue || x.Id != excludeSiteId.Value),
            cancellationToken);
    }

    public Task AddSiteAsync(ExternalSite site, CancellationToken cancellationToken)
    {
        return _context.ExternalSites.AddAsync(site, cancellationToken).AsTask();
    }

    public void RemoveSite(ExternalSite site)
    {
        _context.ExternalSites.Remove(site);
    }

    public Task AddCrawlAsync(ExternalSiteCrawl crawl, CancellationToken cancellationToken)
    {
        return _context.ExternalSiteCrawls.AddAsync(crawl, cancellationToken).AsTask();
    }

    public Task<ExternalSiteCrawl?> GetCrawlByIdWithSiteAsync(Guid crawlId, CancellationToken cancellationToken)
    {
        return _context.ExternalSiteCrawls
            .Include(x => x.ExternalSite)
            .FirstOrDefaultAsync(x => x.Id == crawlId, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
