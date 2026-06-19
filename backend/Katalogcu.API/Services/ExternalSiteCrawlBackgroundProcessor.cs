using Hangfire;
using Katalogcu.Application.Common.Interfaces;
using Katalogcu.Domain.Entities;

namespace Katalogcu.API.Services;

public sealed class ExternalSiteCrawlBackgroundProcessor : IExternalSiteCrawlBackgroundProcessor
{
    private readonly IExternalSiteRepository _externalSiteRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<ExternalSiteCrawlBackgroundProcessor> _logger;

    public ExternalSiteCrawlBackgroundProcessor(
        IExternalSiteRepository externalSiteRepository,
        IBackgroundJobClient backgroundJobClient,
        ILogger<ExternalSiteCrawlBackgroundProcessor> logger)
    {
        _externalSiteRepository = externalSiteRepository;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
    }

    public async Task<Guid> EnqueueAsync(Guid externalSiteId, CancellationToken cancellationToken)
    {
        var crawl = new ExternalSiteCrawl
        {
            Id = Guid.NewGuid(),
            ExternalSiteId = externalSiteId,
            TriggerType = "manual",
            ExecutionMode = "fetch",
            Status = "queued",
            CreatedDate = DateTime.UtcNow
        };

        await _externalSiteRepository.AddCrawlAsync(crawl, cancellationToken);
        await _externalSiteRepository.SaveChangesAsync(cancellationToken);

        var jobId = _backgroundJobClient.Enqueue<ExternalSiteCrawlHangfireJob>(
            job => job.ExecuteAsync(crawl.Id, CancellationToken.None));

        _logger.LogInformation("External site crawl job kuyruğa eklendi: {ExternalSiteId} | CrawlId={CrawlId} | JobId={JobId}",
            externalSiteId,
            crawl.Id,
            jobId);

        return crawl.Id;
    }
}
