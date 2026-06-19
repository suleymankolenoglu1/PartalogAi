namespace Katalogcu.Application.Common.Interfaces;

public interface IExternalSiteCrawlOrchestrator
{
    Task ExecuteAsync(Guid crawlId, CancellationToken cancellationToken);
}
