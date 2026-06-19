namespace Katalogcu.Application.Common.Interfaces;

public interface IExternalSiteCrawlBackgroundProcessor
{
    Task<Guid> EnqueueAsync(Guid externalSiteId, CancellationToken cancellationToken);
}
