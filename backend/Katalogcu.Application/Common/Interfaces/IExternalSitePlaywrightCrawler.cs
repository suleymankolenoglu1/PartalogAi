using Katalogcu.Application.Common.Models;

namespace Katalogcu.Application.Common.Interfaces;

public interface IExternalSitePlaywrightCrawler
{
    Task<ExternalSiteFetchResult> CrawlAsync(string baseUrl, CancellationToken cancellationToken);
}
