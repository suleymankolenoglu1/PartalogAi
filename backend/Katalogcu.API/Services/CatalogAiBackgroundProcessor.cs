using Katalogcu.Application.Common.Interfaces;
using Hangfire;
using Microsoft.Extensions.Options;

namespace Katalogcu.API.Services;

public sealed class CatalogAiBackgroundProcessor : ICatalogAiBackgroundProcessor
{
    private readonly ICatalogAiJobRepository _catalogAiJobRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly ILogger<CatalogAiBackgroundProcessor> _logger;
    private readonly int _maxAttempts;

    public CatalogAiBackgroundProcessor(
        ICatalogAiJobRepository catalogAiJobRepository,
        IBackgroundJobClient backgroundJobClient,
        IOptions<CatalogAiProcessingOptions> options,
        ILogger<CatalogAiBackgroundProcessor> logger)
    {
        _catalogAiJobRepository = catalogAiJobRepository;
        _backgroundJobClient = backgroundJobClient;
        _logger = logger;
        _maxAttempts = options.Value.GetNormalizedMaxAttempts();
    }

    public async Task EnqueueAsync(Guid catalogId, CancellationToken cancellationToken)
    {
        await _catalogAiJobRepository.UpsertPendingAsync(catalogId, _maxAttempts, cancellationToken);
        var backgroundJobId = _backgroundJobClient.Enqueue<CatalogAiHangfireJob>(
            job => job.ExecuteAsync(catalogId, CancellationToken.None));

        _logger.LogInformation(
            "Catalog AI Hangfire job kuyruğa eklendi: {CatalogId} | JobId={JobId} | MaxAttempts={MaxAttempts}",
            catalogId,
            backgroundJobId,
            _maxAttempts);
    }
}
