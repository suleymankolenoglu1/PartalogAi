using Katalogcu.Application.Common.Models;

namespace Katalogcu.Application.Common.Interfaces;

public interface IExternalSiteFetchCrawler
{
    Task<ExternalSiteFetchResult> CrawlAsync(string baseUrl, CancellationToken cancellationToken);
}
