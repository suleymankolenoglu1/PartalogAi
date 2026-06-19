using Katalogcu.Domain.Entities;

namespace Katalogcu.Application.Common.Interfaces;

public interface IExternalSiteRepository
{
    Task<IReadOnlyList<ExternalSite>> GetSitesByUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<Dictionary<Guid, ExternalSiteCrawl>> GetLatestCrawlsBySiteIdsAsync(IReadOnlyCollection<Guid> siteIds, CancellationToken cancellationToken);
    Task<ExternalSite?> GetSiteByIdAsync(Guid siteId, Guid userId, CancellationToken cancellationToken);
    Task<ExternalSite?> GetSiteByIdAsync(Guid siteId, CancellationToken cancellationToken);
    Task<(IReadOnlyList<ExternalProduct> Products, int TotalCount)> GetProductsBySiteAsync(Guid siteId, Guid userId, int skip, int take, CancellationToken cancellationToken);
    Task<bool> BaseUrlExistsAsync(Guid userId, string baseUrl, Guid? excludeSiteId, CancellationToken cancellationToken);
    Task AddSiteAsync(ExternalSite site, CancellationToken cancellationToken);
    void RemoveSite(ExternalSite site);
    Task AddCrawlAsync(ExternalSiteCrawl crawl, CancellationToken cancellationToken);
    Task<ExternalSiteCrawl?> GetCrawlByIdWithSiteAsync(Guid crawlId, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
