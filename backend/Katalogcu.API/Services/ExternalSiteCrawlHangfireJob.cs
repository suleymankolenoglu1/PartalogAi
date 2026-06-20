using Hangfire;
using Katalogcu.Application.Common.Interfaces;

namespace Katalogcu.API.Services;

public sealed class ExternalSiteCrawlHangfireJob
{
    public const string QueueName = "external-site-crawl";

    private readonly IExternalSiteCrawlOrchestrator _orchestrator;

    public ExternalSiteCrawlHangfireJob(IExternalSiteCrawlOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    [Queue(QueueName)]
    public Task ExecuteAsync(Guid crawlId, CancellationToken cancellationToken)
    {
        return _orchestrator.ExecuteAsync(crawlId, cancellationToken);
    }
}
